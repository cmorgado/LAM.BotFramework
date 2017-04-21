using System;
using System.Threading.Tasks;
using LAM.BotFramework.Code;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using LAM.BotFramework.Entities;

namespace LAM.BotFramework.Dialogs
{
    /// <summary>
    /// Main Dialog
    /// </summary>
    [Serializable]
    public class MainDialog : IDialog<object>
    {
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);
            return Task.CompletedTask;
        }

        public async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> argument)
        {
            IMessageActivity argumentaMessageActivity = await argument;
            string json = Scenario.LoadRecentScenario(Global.ScenarioName);
            if (string.IsNullOrEmpty(json))
            {
                var reply = context.MakeMessage();
                reply.Text = "No scenario definition:" + Global.ScenarioName;
                await context.PostAsync(reply);
            }
            else
            {
                Question question = new Question(context);
                await question.Initialize(json, argumentaMessageActivity.Text);
            }
        }
    }
}

