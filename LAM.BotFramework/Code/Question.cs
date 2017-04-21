using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using LAM.BotFramework.Entities;
using LAM.BotFramework.Helpers;
using LAM.BotFramework.ServiceConnectors;
using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Newtonsoft.Json;

namespace LAM.BotFramework.Code
{
    [Serializable]
    public partial class Question
    {
        #region Properties
        [NonSerialized]
        IDialogContext _context;
        [NonSerialized]
        public string LogToken = "";
        public string RetryPrompt { get; set; }
        public int CurrentQuestion
        {
            get
            {
                int currentStepId;
                _context.PrivateConversationData.TryGetValue("CurrentQuestion", out currentStepId);
                return currentStepId;
            }
            set
            {
                _context.PrivateConversationData.SetValue("CurrentQuestion", value);
            }
        }
        public Dictionary<string, string> Properties
        {
            get
            {
                Dictionary<string, string> properties = null;
                _context?.PrivateConversationData.TryGetValue("Properties", out properties);
                return properties;
            }
            set
            {
                _context.PrivateConversationData.SetValue("Properties", value);
            }
        }

        private void PropertiesStore(string varName, string value)
        {
            if (!string.IsNullOrEmpty(varName))
            {
                Dictionary<string, string> properties = this.Properties ?? new Dictionary<string, string>();
                properties[varName.ToLower()] = value;
                this.Properties = properties;
            }
        }
        public void SetScenario(string scenario)
        {
            _context.PrivateConversationData.SetValue("ScenarioData", scenario);
        }
        public List<QuestionRow> Questions()
        {
            var json = "";
            _context.PrivateConversationData.TryGetValue("ScenarioData", out json);

            return JsonConvert.DeserializeObject<List<QuestionRow>>(json);
        }
        [NonSerialized]
        public QuestionRow CurrentQuestionRow;
        #endregion

        public Question() { }
        public Question(IDialogContext context)
        {
            _context = context;
        }

        public async Task Initialize(string json)
        {
            await this.Initialize(json, null);
        }
        public async Task Initialize(string json, string message)
        {
            this.LogToken = Global.ScenarioName;

            //this.LogToken = LogToken;
            StackReset("main");
            SetScenario(json);
            CurrentQuestion = 0;
            Properties = new Dictionary<string, string>();

            QuestionRow nextQ = JsonConvert.DeserializeObject<List<QuestionRow>>(json)[0];

            Load(nextQ);
            await this.Execute(_context,message);
        }

        public async Task Execute(IDialogContext context, string message)
        {
            Dictionary<string, string> d = this.Properties;
            if (CurrentQuestionRow.BypassNode=="Yes")
            {
                //GET VARIABLE NAME
                if (d.ContainsKey(CurrentQuestionRow.NodeName.ToLower()))
                {
                    var value = d[CurrentQuestionRow.NodeName.ToLower()];
                    //IF EXISTS, PROCESS IT
                    if (!string.IsNullOrEmpty(value))
                    {
                        await ProcessResponse(context, value, null);
                        return;
                    }
                }
            }

            #region KEY REPLACEMENT
            //REPLACE KEYS STATED WITH PragmaOpen and PragmaClose
            foreach (var item in d)
            {
                CurrentQuestionRow.QuestionText = ReplaceString(CurrentQuestionRow.QuestionText, Global.PragmaOpen + item.Key.ToUpper() + Global.PragmaClose, item.Value, StringComparison.CurrentCultureIgnoreCase);
                CurrentQuestionRow.Options = ReplaceString(CurrentQuestionRow.Options, Global.PragmaOpen + item.Key.ToUpper() + Global.PragmaClose, item.Value, StringComparison.CurrentCultureIgnoreCase);
                CurrentQuestionRow.NextQ = ReplaceString(CurrentQuestionRow.NextQ, Global.PragmaOpen + item.Key.ToUpper() + Global.PragmaClose, item.Value, StringComparison.CurrentCultureIgnoreCase);
            }
            CurrentQuestionRow.QuestionText = CurrentQuestionRow.QuestionText.Replace("<br>", "\n\n\n");
            #endregion

            #region TRANSLATION

            string language = GetLanguage(context);
            string promptTranslated = CurrentQuestionRow.QuestionText;
            if (CurrentQuestionRow.LangDet == "Yes" && !string.IsNullOrEmpty(message))
            {
                string detectedLanguage = Translator.Detect(message);
                if (detectedLanguage != language)
                {
                    SetLanguage(context, detectedLanguage);
                }
            }
            #endregion
            promptTranslated = Translator.Translate(CurrentQuestionRow.QuestionText, "en", language);

            //FOR TESTING:
            //string s = "[{'type':'Hero','title':'Im the points card bot, how can I help you?','subtitle':'','text':'','imageURL':'http://lambot.azurewebsites.net/Images/botman.png','action':[{'type': 'ImBack','title': 'Check Points','value': 'How many points do I have?'},{'type': 'ImBack','title': 'Redeem points','value': 'I want to redeem points'},{'type': 'ImBack','title': 'Transfer points','value': 'I want to transfer points'}]}]";
            //CurrentQuestionRow.QuestionText = s;

            #region HANDLE HERO info
            string logPrompt = promptTranslated;
            bool bHasHero = false;
            if (CurrentQuestionRow.QuestionText.IndexOf("{") == 0)
            {
                await HeroCardPrompt(context, language);
                logPrompt = CurrentQuestionRow.QuestionText;
                CurrentQuestionRow.QuestionText = "";
                promptTranslated = "";
                bHasHero = true;
            }
            if (CurrentQuestionRow.QuestionText.IndexOf("[") == 0)
            {
                await CarouselCardPrompt(context, language);
                logPrompt = CurrentQuestionRow.QuestionText;
                CurrentQuestionRow.QuestionText = "";
                promptTranslated = "";
                bHasHero = true;
            }
            #endregion

            if (!string.IsNullOrEmpty(message) && (CurrentQuestionRow.BypassNode=="Yes" || CurrentQuestion==0))
            {
                #region HANDLE BYPASS
                switch (CurrentQuestionRow.QuestionType)
                {
                    case "LUIS":
                        string resultT = message;
                        string resultTranslated = resultT;
                        if (language != "en")
                        {
                            resultTranslated = Translator.Translate(resultT, language, "en");
                        }

                        await ProcessResponseLuis(context, resultTranslated);
                        break;
                    case "QnAMaker":
                        await ProcessResponseQnAMaker(context, message);
                        break;
                    case "Search":
                        await ProcessResponseSearch(context, message);
                        break;
                    default:
                        await ProcessResponse(context, message,null);
                        break;
                }
                #endregion
            }
            else
            {
                ConversationLog.Log(context, "BOT", logPrompt, LogToken, CurrentQuestion);

                #region HANDLE QUESTIONTYPES
                switch (CurrentQuestionRow.QuestionType)
                {
                    case "API":
                        //GET
                        string json = await REST.Get(CurrentQuestionRow.QuestionText, true);

                        Dictionary<string, string> list = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);

                        //READ RETURN DICTIONARY
                        foreach (var item in list)
                        {
                            PropertiesStore(item.Key, item.Value);
                        }

                        await ProcessResponse(context, "", null);
                        break;
                    case "APIFULL":
                        //SET THE CONTEXT
                        IMessageActivity dummyReply = context.MakeMessage();
                        BotProps bp = new BotProps {Properties = this.Properties};
                        string st1 = this.CurrentQuestionRow.NextQ;
                        int nq = 0;
                        if (st1.IndexOf('{') > -1)
                        {
                            st1 = st1.Replace("'", "\"");
                            List<NextQuestion> lnq = JsonConvert.DeserializeObject<List<NextQuestion>>(st1);
                            nq = lnq[0].q;
                        }
                        else
                        {
                            nq = int.Parse(st1);
                        }

                        bp.NextQuestion = nq;
                        bp.Scenario = this.Questions();
                        //BP.ResCookie = new ConversationReference(dummyReply);
                        bp.Result = "";

                        int? nextQ = null;
                        try
                        {
                            //CALL IT
                            bp = await REST.Post(CurrentQuestionRow.QuestionText, bp);

                            Properties = bp.Properties;
                            if (bp.NextQuestion != nq)
                                nextQ = bp.NextQuestion;
                            if (bp.Scenario != null)
                                this.SetScenario(JsonConvert.SerializeObject(bp.Scenario));
                        }
                        catch (Exception)
                        {

                        }

                        await ProcessResponse(context, bp.Result, nextQ);
                        break;
                    case "SUB":
                        //ADD TO THE STACK
                        StackPush(CurrentQuestionRow.Sub, this.CurrentQuestion);
                        //MOVE TO THE FIRST OF THE SUB
                        //                    GOTO CurrentQuestionRow.options
                        int nextq = int.Parse(CurrentQuestionRow.Options);
                        //AT END OF THE SUB, RETURN TO THE STACK
                        await ProcessResponse(context, "", nextq);
                        break;
                    case "Expression":
                        string sRes = "Yes";
                        if (CurrentQuestionRow.QuestionText.IndexOf(Global.PragmaOpen) > -1 && CurrentQuestionRow.QuestionText.IndexOf(Global.PragmaClose) > -1)
                        {
                            sRes = "No";
                        }
                        else
                        {
                            var result = CSharpScript.EvaluateAsync(CurrentQuestionRow.QuestionText).Result;
                            if (result.GetType().Name == "String")
                                sRes = "Yes";
                            else
                            {
                                if ((bool)result)
                                    sRes = "Yes";
                                else
                                    sRes = "No";
                            }
                        }
                        await ProcessResponse(context, sRes, null);
                        break;
                    case "LUIS":
                        if (bHasHero)
                        {
                            context.Wait(ProcessResponseLuisBypass);
                        }
                        else
                            PromptDialog.Text(context,
                                                MessageLoopLuis,
                                                promptTranslated,
                                                RetryPrompt);
                        break;
                    case "Text":
                        if (bHasHero)
                        {
                            context.Wait(ProcessResponseBypass);
                        }
                        else
                            PromptDialog.Text(context,
                                            MessageLoop,
                                            promptTranslated,
                                            RetryPrompt);
                        break;
                    case "Carousel":
                        var reply = context.MakeMessage();
                        reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                        //type,title,subtitle,text,urlforimage,urltoopen

                        List<AttachmentCard> opat = JsonConvert.DeserializeObject<List<AttachmentCard>>(CurrentQuestionRow.Options.Replace("'", "\""));
                        IList<Attachment> cardsAttachment = new List<Attachment>();

                        foreach (AttachmentCard item in opat)
                        {
                            string actiontype = "";
                            switch (item.action.type.ToLower())
                            {
                                case "openurl":
                                    actiontype = ActionTypes.OpenUrl;
                                    break;
                                case "imback":
                                    actiontype = ActionTypes.ImBack;
                                    break;
                                default:
                                    actiontype = ActionTypes.OpenUrl;
                                    break;
                            }
                            CardAction ca = new CardAction(actiontype, item.action.title, value: item.action.value);
                            CardImage ci = new CardImage(url: item.imageURL);

                            if (item.type == "Hero")
                            {
                                cardsAttachment.Add(
                                    Bot.GetHeroCard(item.title, item.subtitle, item.text, new List<CardImage>() { ci }, new List<CardAction>() { ca })
                                );
                            }
                            if (item.type == "Thumbnail")
                            {
                                cardsAttachment.Add(
                                    Bot.GetThumbnailCard(item.title, item.subtitle, item.text, ci, ca)
                                );
                            }
                        }
                        reply.Attachments = cardsAttachment;
                        await context.PostAsync(reply);
                        //await ProcessResponse(context, "");
                        PromptDialog.Text(context,
                                            MessageLoop,
                                            promptTranslated,
                                            RetryPrompt);
                        break;
                    case "QnAMaker":
                        string sjson = CurrentQuestionRow.Options.Replace("'", "\"");
                        OptionsQnAMaker oq = JsonConvert.DeserializeObject<OptionsQnAMaker>(sjson);
                        if (string.IsNullOrEmpty(oq.QSearch))
                        {
                            if (bHasHero)
                            {
                                context.Wait(ProcessResponseQnABypass);
                            }
                            else
                                PromptDialog.Text(context,
                                                    MessageLoopQnAMaker,
                                                    promptTranslated,
                                                    RetryPrompt
                                                    );
                        }
                        else
                        {
                            //the QSearch should have the Pragmas by default.
                            string result = KeyReplace(Global.PragmaOpen + oq.QSearch + Global.PragmaClose);
                            if (result.IndexOf("{") == 0)
                            {
                                LUISresultv2 LRV2 = JsonConvert.DeserializeObject<LUISresultv2>(result);
                                result = LRV2.query;
                            }
                            await ProcessResponseQnAMaker(context, result);
                        }
                        break;
                    case "Search":
                        string sJsonS = CurrentQuestionRow.Options.Replace("'", "\"");
                        OptionsSearch OS = JsonConvert.DeserializeObject<OptionsSearch>(sJsonS);
                        if (string.IsNullOrEmpty(OS.QSearch))
                        {
                            if (bHasHero)
                            {
                                context.Wait(ProcessResponseSearchBypass);
                            }
                            else
                                PromptDialog.Text(context,
                                                    MessageLoopSearch,
                                                    promptTranslated,
                                                    RetryPrompt
                                                    );
                        }
                        else
                        {
                            string result = KeyReplace(Global.PragmaOpen + OS.QSearch + Global.PragmaClose);
                                if (result.IndexOf("{") == 0)
                                {
                                    LUISresultv2 LRV2 = JsonConvert.DeserializeObject<LUISresultv2>(result);
                                    result = LRV2.query;
                                }
                                await ProcessResponseSearch(context, result);
                        }
                        break;
                    case "Choice":
                        string[] op = CurrentQuestionRow.Options.Split(',');
                        PromptDialog.Choice(context,
                                                MessageLoop,
                                                op,
                                                promptTranslated,
                                                RetryPrompt,
                                                promptStyle: PromptStyle.Auto);
                        break;
                    case "ChoiceAction":
                        string[] opA = CurrentQuestionRow.Options.Split(',');
                        if (CurrentQuestionRow.Options == "")
                        {
                            string st = CurrentQuestionRow.NextQ;
                            if (!string.IsNullOrEmpty(st))
                            {
                                if (st.IndexOf('{') > -1)
                                {
                                    st = st.Replace("'", "\"");
                                    List<NextQuestion> lnq = JsonConvert.DeserializeObject<List<NextQuestion>>(st);
                                    opA = new string[lnq.Count];
                                    for (var i = 0; i < lnq.Count; i++)
                                    {
                                        opA[i] = lnq[i].intent;
                                    }
                                }
                            }

                        }

                        PromptDialog.Choice(context,
                                                MessageLoop,
                                                opA,
                                                promptTranslated,
                                                RetryPrompt,
                                                promptStyle: PromptStyle.Auto);
                        break;
                    case "Hero":
                        var replyH = context.MakeMessage();
                        //type,title,subtitle,text,urlforimage,urltoopen
                        AttachmentHero item1 = JsonConvert.DeserializeObject<AttachmentHero>(CurrentQuestionRow.QuestionText.Replace("'", "\""));

                        List<CardAction> lca = new List<CardAction>();
                        foreach (var actions in item1.action)
                        {
                            string actiontype = "";
                            switch (actions.type)
                            {
                                case "OpenURL":
                                    actiontype = ActionTypes.OpenUrl;
                                    break;
                                case "ImBack":
                                    actiontype = ActionTypes.ImBack;
                                    break;
                                default:
                                    actiontype = ActionTypes.ImBack;
                                    break;
                            }
                            CardAction CA = new CardAction(actiontype, actions.title, value: actions.value);
                            lca.Add(CA);
                        }

                        replyH.Attachments = new List<Attachment>
                        {
                            Bot.GetHeroCard(item1.title, item1.subtitle, item1.text, new List<CardImage>() { new CardImage(url: item1.imageURL) }, lca)
                        };
                        await context.PostAsync(replyH);
                        await ProcessResponse(context, "", null);
                        break;
                    case "Boolean":
                        if (bHasHero)
                        {
                            context.Wait(ProcessResponseBypass);
                        }
                        else
                            PromptDialog.Confirm(context,
                                            MessageLoop,
                                            promptTranslated,
                                            RetryPrompt);
                        break;
                    case "Integer":
                        if (bHasHero)
                        {
                            context.Wait(ProcessResponseBypass);
                        }
                        else
                            PromptDialog.Number(context,
                                            MessageLoop,
                                            promptTranslated,
                                            RetryPrompt);
                        break;
                    case "Message":
                        if (!bHasHero)
                        {
                            await context.PostAsync(promptTranslated);
                        }
                        await ProcessResponse(context, "", null);
                        break;
                    case "MessageEnd":
                        if (!bHasHero)
                        {
                            await context.PostAsync(promptTranslated);
                        }

                        context.Done(true);
                        break;
                    case "ResetAllVars":
                        this.Properties=new Dictionary<string, string>();
                        await ProcessResponse(context, "", null);
                        break;
                    default:
                        break;
                }
                #endregion
            }
        }

        private string KeyReplace(string text)
        {
            //REPLACE KEYS STATED WITH PragmaOpen and PragmaClose
            Dictionary<string, string> D = this.Properties;
            foreach (var item in D)
            {
                text = ReplaceString(text, Global.PragmaOpen + item.Key.ToUpper() + Global.PragmaClose, item.Value, StringComparison.CurrentCultureIgnoreCase);
            }
            return text;
        }
        
        #region StackManagement
        private void StackReset(string value)
        {
            List<StackItem> stack = new List<StackItem> {new StackItem() {sub = value, nextQ = 0}};

            string existingStack = JsonConvert.SerializeObject(stack);
            _context.PrivateConversationData.SetValue("SubStack", existingStack);
        }
        private void StackPush(string value, int questionNumber)
        {
            string existingStack = "";
            _context.PrivateConversationData.TryGetValue("SubStack", out existingStack);
            List<StackItem> stack = JsonConvert.DeserializeObject<List<StackItem>>(existingStack);

            stack.Add(new StackItem() { sub = value, nextQ = questionNumber });

            existingStack = JsonConvert.SerializeObject(stack);
            _context.PrivateConversationData.SetValue("SubStack", existingStack);
        }
        private int StackPop()
        {
            string existingStack = "";
            _context.PrivateConversationData.TryGetValue("SubStack", out existingStack);
            List<StackItem> stack = JsonConvert.DeserializeObject<List<StackItem>>(existingStack);

            StackItem value = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);

            existingStack = JsonConvert.SerializeObject(stack);
            _context.PrivateConversationData.SetValue("SubStack", existingStack);
            return value.nextQ;
        }
        #endregion

        private async Task HeroCardPrompt(IDialogContext context, string language)
        {

            //{ 'subtitle': 'aa','text': 'aa','imageURL': 'http://lambot.azurewebsites.net/Images/cardgold.png',  'action': [    {      'type': 'ImBack',      'title': 'aaa',      'value': 'bbb'    },    {      'type': 'ImBack',      'title': 'aaa2',      'value': 'bbb2'    }  ]}
            var replyH = context.MakeMessage();
            //type,title,subtitle,text,urlforimage,urltoopen
            AttachmentHero item1 = JsonConvert.DeserializeObject<AttachmentHero>(CurrentQuestionRow.QuestionText.Replace("'", "\""));

            string iti =  Translator.Translate(item1.title, "en", language);
            string isu = Translator.Translate(item1.subtitle, "en", language);
            string ite = Translator.Translate(item1.text, "en", language);

            List<CardAction> lca = new List<CardAction>();
            foreach (var actions in item1.action)
            {
                string actiontype = "";
                switch (actions.type.ToLower())
                {
                    case "openurl":
                        actiontype = ActionTypes.OpenUrl;
                        break;
                    case "imback":
                        actiontype = ActionTypes.ImBack;
                        break;
                    default:
                        actiontype = ActionTypes.ImBack;
                        break;
                }
                string ati = Translator.Translate(actions.title, "en", language);
                string ava = Translator.Translate(actions.value, "en", language);
                CardAction ca = new CardAction(actiontype, ati, value: ava);
                lca.Add(ca);
            }

            replyH.Attachments = new List<Attachment>
                    {
                        Bot.GetHeroCard(iti, isu, ite,new List<CardImage>() {  new CardImage(url: item1.imageURL) }, lca)
                    };
            await context.PostAsync(replyH);
        }
        private async Task CarouselCardPrompt(IDialogContext context, string language)
        {
            var replyH = context.MakeMessage();

            AttachmentHero[] items = JsonConvert.DeserializeObject<AttachmentHero[]>(CurrentQuestionRow.QuestionText.Replace("'", "\""));
            if (items.Length>1)
                replyH.AttachmentLayout = AttachmentLayoutTypes.Carousel;

            //{ 'subtitle': 'aa','text': 'aa','imageURL': 'http://lambot.azurewebsites.net/Images/cardgold.png',  'action': [    {      'type': 'ImBack',      'title': 'aaa',      'value': 'bbb'    },    {      'type': 'ImBack',      'title': 'aaa2',      'value': 'bbb2'    }  ]}
            //type,title,subtitle,text,urlforimage,urltoopen
            List<Attachment> la = new List<Attachment>();
            foreach (AttachmentHero item in items)
            {
                string iti = Translator.Translate(item.title, "en", language);
                string isu = Translator.Translate(item.subtitle, "en", language);
                string ite = Translator.Translate(item.text, "en", language);

                List<CardAction> lca = new List<CardAction>();
                foreach (var actions in item.action)
                {
                    string actiontype = "";
                    switch (actions.type.ToLower())
                    {
                        case "openurl":
                            actiontype = ActionTypes.OpenUrl;
                            break;
                        case "imback":
                            actiontype = ActionTypes.ImBack;
                            break;
                        default:
                            actiontype = ActionTypes.ImBack;
                            break;
                    }
                    string ati = Translator.Translate(actions.title, "en", language);
                    string ava = Translator.Translate(actions.value, "en", language);

                    CardAction ca = new CardAction(actiontype, ati, value: ava);
                    lca.Add(ca);
                }
                la.Add(
                    Bot.GetHeroCard(iti, isu, ite,new List<CardImage>() {  new CardImage(url: item.imageURL) }, lca)
                 );

            }

            replyH.Attachments = la;
            await context.PostAsync(replyH);
        }

        #region MessageLoop
        public async Task MessageLoop(IDialogContext context, IAwaitable<string> message)
        {
            string result = await message;

            await ProcessResponse(context, result, null);
        }
        public async Task MessageLoop(IDialogContext context, IAwaitable<bool> message)
        {
            string result = (await message).ToString();

            await ProcessResponse(context, result, null);
        }
        public async Task MessageLoop(IDialogContext context, IAwaitable<long> message)
        {
            string result = (await message).ToString();

            await ProcessResponse(context, result, null);
        }
        public async Task MessageLoopLuis(IDialogContext context, IAwaitable<string> message)
        {
            string result = await message;

            string language = GetLanguage(context);
            string resultTranslated = Translator.Translate(result, language, "en");

            await ProcessResponseLuis(context, resultTranslated);
        }
        public async Task MessageLoopSearch(IDialogContext context, IAwaitable<string> message)
        {
            string result = await message;

            await ProcessResponseSearch(context, result);
        }
        public async Task MessageLoopQnAMaker(IDialogContext context, IAwaitable<string> message)
        {
            string result = await message;

            await ProcessResponseQnAMaker(context, result);
        }
        #endregion

        #region ProcessResponse
        private async Task ProcessResponseLuisBypass(IDialogContext context, IAwaitable<object> result)
        {
            Activity act = (await result) as Activity;

            string language = GetLanguage(context);
            string resultTranslated = Translator.Translate(act.Text, language, "en");

            await ProcessResponseLuis(context, resultTranslated);
        }
        private async Task ProcessResponseQnABypass(IDialogContext context, IAwaitable<object> result)
        {
            Activity act = (await result) as Activity;

            string language = GetLanguage(context);
            string resultTranslated = Translator.Translate(act.Text, language, "en");

            await ProcessResponseQnAMaker(context, resultTranslated);
        }
        private async Task ProcessResponseSearchBypass(IDialogContext context, IAwaitable<object> result)
        {
            Activity act = (await result) as Activity;

            string language = GetLanguage(context);
            string resultTranslated = Translator.Translate(act.Text, language, "en");

            await ProcessResponseSearch(context, resultTranslated);
        }
        private async Task ProcessResponseBypass(IDialogContext context, IAwaitable<object> result)
        {
            Activity act = (await result) as Activity;

            string language = GetLanguage(context);
            string resultTranslated = Translator.Translate(act.Text, language, "en");

            await ProcessResponse(context, resultTranslated, null);
        }
        protected async Task ProcessResponse(IDialogContext context, string result, int? ForceNextQ)
        {
            Question q = new Question(context);
            ConversationLog.Log(context, "USER", result, LogToken, q.CurrentQuestion);
            List<QuestionRow> lqj = q.Questions();
            QuestionRow qj = lqj[q.CurrentQuestion];

            //Store variable
            q.PropertiesStore(qj.NodeName, result);

            if (ForceNextQ != null && ForceNextQ != -1)
                q.CurrentQuestion = (int)ForceNextQ;
            else
                q.CurrentQuestion++;

            if (ForceNextQ == null)
                q.MoveNextStep(qj, result);

            if (q.CurrentQuestion >= lqj.Count)
            {
                context.Done(true);
            }
            else
            {
                //NEXT QUESTION
                q.Load(lqj[q.CurrentQuestion]);
                await q.Execute(context,null);
            }
        }
        protected async Task ProcessResponseQnAMaker(IDialogContext context, string result)
        {
            Question q = new Question(context);
            ConversationLog.Log(context, "USER", result, LogToken, q.CurrentQuestion);

            List<QuestionRow> lqj = q.Questions();
            QuestionRow qj = lqj[q.CurrentQuestion];

            q.PropertiesStore(qj.NodeName, result);

            OptionsQnAMaker oq = JsonConvert.DeserializeObject<OptionsQnAMaker>(qj.Options.Replace("'", "\""));

            QnAMaker.QnAMakerResult r = ServiceConnectors.QnAMaker.Get(oq.KBId, oq.Key, result);

            double oqms = 0;
            double.TryParse(oq.MinScore, out oqms);

            await Render.QnAMaker(context, oq.NotFoundMessage, r, oqms);

            q.CurrentQuestion++;
            q.MoveNextStep(qj, "");
            if (q.CurrentQuestion >= lqj.Count)
            {
                //QDone(this, new QuestionEventArgs(-1));
                context.Done(true);
            }
            else
            {
                //QDone(this, new QuestionEventArgs(Q.CurrentQuestion));

                q.Load(lqj[q.CurrentQuestion]);
                await q.Execute(context, null);
            }
        }

        protected async Task ProcessResponseSearch(IDialogContext context, string result)
        {
            Question q = new Question(context);
            ConversationLog.Log(context, "USER", result, LogToken, q.CurrentQuestion);

            List<QuestionRow> lqj = q.Questions();
            QuestionRow qj = lqj[q.CurrentQuestion];

            q.PropertiesStore(qj.NodeName, result);

            OptionsSearch os = JsonConvert.DeserializeObject<OptionsSearch>(qj.Options.Replace("'", "\""));

            SearchServiceClient serviceClient = new SearchServiceClient(os.ServiceName, new SearchCredentials(os.Key));

            var parameters = new SearchParameters()
            {
                Select = new[] { os.FieldQ, os.FieldA },
                HighlightFields = new[] { os.FieldQ }
            };

            ISearchIndexClient indexClient = serviceClient.Indexes.GetClient(os.Index);
            DocumentSearchResult<object> searchResults = indexClient.Documents.Search<object>(result, parameters);

            await Render.Search(context, os, searchResults);

            q.CurrentQuestion++;
            q.MoveNextStep(qj, "");
            if (q.CurrentQuestion >= lqj.Count)
            {
                //QDone(this, new QuestionEventArgs(-1));
                context.Done(true);
            }
            else
            {
                //QDone(this, new QuestionEventArgs(Q.CurrentQuestion));

                q.Load(lqj[q.CurrentQuestion]);
                await q.Execute(context, null);
            }
        }

        protected async Task ProcessResponseLuis(IDialogContext context, string result)
        {
            Question q = new Question(context);
            int currentStepId = q.CurrentQuestion;
            ConversationLog.Log(context, "USER", result, LogToken, q.CurrentQuestion);

            List<QuestionRow> lqj = q.Questions();
            QuestionRow qj = lqj[currentStepId];


            string sUrl = qj.Options;
            string intent = "";

            try
            {
                var lresult = await LUIS.getLUISresult(sUrl, result);

                //v2
                LUISresultv2 lRv2 = JsonConvert.DeserializeObject<LUISresultv2>(lresult);
                if (lRv2.topScoringIntent?.score > 0.3)
                {
                    intent = lRv2.topScoringIntent.intent;
                    //STORE ENTITIES
                    foreach (LUISentities item in lRv2.entities)
                    {
                        if (item.score > 0.3)
                        {
                            q.PropertiesStore(item.type, item.entity);
                        }
                    }

                }
                result = lresult;


                if (intent != "")
                {
                    //await context.PostAsync("Debug message - your intent was: " + intent);
                }
                else
                {
                    await context.PostAsync("No intent found.");
                    intent = "None";
                }
            }
            catch (Exception e)
            {
                await context.PostAsync("LUIS ERROR:\n" + e.InnerException.Message);
                context.Done<object>(null);
                return;
            }

            if (intent != "")
            {
                q.PropertiesStore(qj.NodeName, result);

                currentStepId++;
                if (currentStepId >= lqj.Count)
                {
                    context.Done<object>(null);
                }
                else
                {
                    if (!string.IsNullOrEmpty(qj.NextQ))
                    {
                        if (qj.NextQ.IndexOf("[") >= 0)
                        {
                            List<NextQuestion> lnq = JsonConvert.DeserializeObject<List<NextQuestion>>(qj.NextQ.Replace("'", "\""));
                            foreach (NextQuestion item in lnq)
                            {
                                if (item.intent == intent)
                                {
                                    currentStepId = item.q;
                                }
                            }
                        }
                        else
                        {
                            currentStepId = int.Parse(qj.NextQ);
                        }
                    }

                    q.CurrentQuestion = currentStepId;
                    q.Load(lqj[currentStepId]);
                    await q.Execute(context,null);
                }
            }
        }

        private void MoveNextStep(QuestionRow qj, string result)
        {
            string st = qj.NextQ;
            if (!string.IsNullOrEmpty(st))
            {
                if (st.IndexOf('{') > -1)
                {
                    st = st.Replace("'", "\"");
                    List<NextQuestion> lnq = JsonConvert.DeserializeObject<List<NextQuestion>>(st);
                    foreach (NextQuestion item in lnq)
                    {
                        if (item.intent == result || item.intent == "")
                        {
                            this.CurrentQuestion = item.q;
                            break;
                        }
                    }
                }
                else
                {
                    try
                    {
                        this.CurrentQuestion = int.Parse(st);

                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            if (this.CurrentQuestion == -1)
            {
                int q = StackPop();
                List<QuestionRow> lqj = this.Questions();
                QuestionRow newQj = lqj[q];
                MoveNextStep(newQj, "");
            }
        }
        public void SetLanguage(IDialogContext context, string language)
        {
            context.PrivateConversationData.SetValue("CurrentLanguage", language);
        }
        public string GetLanguage(IDialogContext context)
        {
            string detectedLanguage = "";
            context.PrivateConversationData.TryGetValue("CurrentLanguage", out detectedLanguage);
            if (string.IsNullOrEmpty(detectedLanguage))
                detectedLanguage = "en";
            return detectedLanguage;
        }
        #endregion

        public void Load(QuestionRow q)
        {
            this.RetryPrompt = "Please try again";
            this.CurrentQuestionRow = q;
        }

        /// <summary>
        /// Replace method that enables StringComparison
        /// </summary>
        /// <param name="str"></param>
        /// <param name="oldValue"></param>
        /// <param name="newValue"></param>
        /// <param name="comparison"></param>
        /// <returns></returns>
        public static string ReplaceString(string str, string oldValue, string newValue, StringComparison comparison)
        {
            var sb = new StringBuilder();

            var previousIndex = 0;
            var index = str.IndexOf(oldValue, comparison);
            while (index != -1)
            {
                sb.Append(str.Substring(previousIndex, index - previousIndex));
                sb.Append(newValue);
                index += oldValue.Length;

                previousIndex = index;
                index = str.IndexOf(oldValue, index, comparison);
            }
            sb.Append(str.Substring(previousIndex));

            return sb.ToString();
        }

    }

}