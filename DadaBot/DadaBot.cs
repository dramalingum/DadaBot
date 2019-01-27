using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text;
using Microsoft.Recognizers.Text.DateTime;
using Microsoft.Recognizers.Text.Number;

namespace DadaBot
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class DadaBot : IBot
    {
        public static readonly string LuisKey = "DadaBot";

        private readonly DadaBotAccessors _accessors;
        private readonly ILogger _logger;
        private readonly BotServices _services;

        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="accessors">A class containing <see cref="IStatePropertyAccessor{T}"/> used to manage state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public DadaBot(DadaBotAccessors accessors, ILoggerFactory loggerFactory, BotServices services)
        {
            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _services = services ?? throw new System.ArgumentNullException(nameof(services));
            if (!_services.LuisServices.ContainsKey(LuisKey))
            {
                throw new System.ArgumentException($"Invalid configuration....");
            }

            _logger = loggerFactory.CreateLogger<DadaBot>();
            _logger.LogTrace("Turn start.");
            _accessors = accessors ?? throw new System.ArgumentNullException(nameof(accessors));
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="Microsoft.Bot.Builder.ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Get the conversation state from the turn context.
                var state = await _accessors.CounterState.GetAsync(turnContext, () => new CounterState());
                var convData = await _accessors.ConversationData.GetAsync(turnContext, () => new ConversationData());
                ConversationFlow flow = await _accessors.ConversationFlow.GetAsync(turnContext, () => new ConversationFlow());
                UserProfile profile = await _accessors.UserProfile.GetAsync(turnContext, () => new UserProfile());

                // Bump the turn count for this conversation.
                state.TurnCount++;

                // Set the property using the accessor.
                await _accessors.CounterState.SetAsync(turnContext, state);
                var text = turnContext.Activity.Text.ToLower();
                if (text == "yellow" || text == "blue" || text == "red")
                    convData.FavouriteColour = text;
                
                await _accessors.ConversationData.SetAsync(turnContext, convData);
                // Save the new turn count into the conversation state.
                await _accessors.ConversationState.SaveChangesAsync(turnContext);

                if (flow.LastQuestionAsked != ConversationFlow.Question.None)
                {
                    await RegisterAsync(flow, profile, turnContext);
                }
                else
                {
                    // Welcome user
                    await Welcome(turnContext);

                    // LUIS Implementation
                    var recognizerResult = await _services.LuisServices[LuisKey].RecognizeAsync(turnContext, cancellationToken);
                    var topIntent = recognizerResult?.GetTopScoringIntent();
                    if (topIntent != null && topIntent.HasValue && topIntent.Value.intent != "None")
                    {
                        await turnContext.SendActivityAsync($"==>LUIS Top Scoring Intent: {topIntent.Value.intent}, Score: {topIntent.Value.score}\n");
                    }
                    else
                    {
                        var msg = @"No LUIS intents were found.
                        This sample is about identifying two user intents:
                        'Calendar.Add'
                        'Calendar.Find'
                        Try typing 'Add Event' or 'Show me tomorrow'.";
                        await turnContext.SendActivityAsync(msg);
                    }
                    // --- End LUIS

                    switch (turnContext.Activity.Text.ToLower())
                    {
                        case "hi":
                            // Standard text
                            await turnContext.SendActivityAsync("Hi, how may I help you today?");
                            break;
                        case "spongebob":
                            // Image
                            var reply = turnContext.Activity.CreateReply();
                            var attachment = new Attachment
                            {
                                ContentUrl = "https://www.telegraph.co.uk/content/dam/TV/2015-09/30sep/spongebob-squarepants.jpg?imwidth=1400",
                                ContentType = "image/jpg",
                                Name = "Spongebob"
                            };
                            reply.Attachments = new List<Attachment>() { attachment };
                            await turnContext.SendActivityAsync(reply, cancellationToken);
                            break;
                        case "buttons":
                            // Buttons
                            var buttonReply = turnContext.Activity.CreateReply("What is your favorite color?");

                            buttonReply.SuggestedActions = new SuggestedActions()
                            {
                                Actions = new List<CardAction>()
                                {
                                    new CardAction() { Title = "Red", Type = ActionTypes.ImBack, Value = "Red", Text = "1"},
                                    new CardAction() { Title = "Yellow", Type = ActionTypes.ImBack, Value = "Yellow", Text = "2" },
                                    new CardAction() { Title = "Blue", Type = ActionTypes.ImBack, Value = "Blue", Text = "3"}
                                },

                            };
                            await turnContext.SendActivityAsync(buttonReply, cancellationToken);
                            break;
                        case "register":
                            await RegisterAsync(flow, profile, turnContext);
                            break;
                        default:
                            var responseMessage = $"Turn {state.TurnCount}: You sent '{turnContext.Activity.Text}'\n";
                            await turnContext.SendActivityAsync(responseMessage);
                            break;
                    }
                }
            }
            else
            {
                // Welcome user logic
                if (turnContext.Activity.MembersAdded.Any())
                {
                    // Iterate over all new members added to the conversation
                    foreach (var member in turnContext.Activity.MembersAdded)
                    {
                        // Greet anyone that was not the target (recipient) of this message
                        // the 'bot' is the recipient for events from the channel,
                        // turnContext.Activity.MembersAdded == turnContext.Activity.Recipient.Id indicates the
                        // bot was added to the conversation.
                        if (member.Id != turnContext.Activity.Recipient.Id)
                        {
                            await Welcome(turnContext);
                            await turnContext.SendActivityAsync($"You are seeing this message because the bot recieved at least one 'ConversationUpdate' event,indicating you (and possibly others) joined the conversation. If you are using the emulator, pressing the 'Start Over' button to trigger this event again. The specifics of the 'ConversationUpdate' event depends on the channel. You can read more information at https://aka.ms/about-botframewor-welcome-user", cancellationToken: cancellationToken);
                        }
                    }
                }
            }
        }


        private async Task RegisterAsync(ConversationFlow flow, UserProfile profile, ITurnContext turnContext)
        {
            // Prompt code
            await FillOutUserProfileAsync(flow, profile, turnContext);

            // Update state and save changes.
            await _accessors.ConversationFlow.SetAsync(turnContext, flow);
            await _accessors.ConversationState.SaveChangesAsync(turnContext);

            await _accessors.UserProfile.SetAsync(turnContext, profile);
            await _accessors.ConversationState.SaveChangesAsync(turnContext);
        }

        private async Task Welcome(ITurnContext turnContext)
        {
            var data = await _accessors.ConversationData.GetAsync(turnContext, () => new ConversationData());

            if (!data.Welcomed)
                await turnContext.SendActivityAsync("Hi, Welcome to DadaBot :) \r\n What's up?");
                data.Welcomed = true;
                await _accessors.ConversationData.SetAsync(turnContext, data);
                await _accessors.ConversationState.SaveChangesAsync(turnContext);
        }


        /// <summary>
        /// Manages the conversation flow for filling out the user's profile, including parsing and validation.
        /// </summary>
        /// <param name="flow">The conversation flow state property.</param>
        /// <param name="profile">The user profile state property.</param>
        /// <param name="turnContext">The context object for the current turn.</param>
        /// <returns>A task that represents the work queued to execute.</returns>
        private static async Task FillOutUserProfileAsync(ConversationFlow flow, UserProfile profile, ITurnContext turnContext)
        {
            string input = turnContext.Activity.Text?.Trim();
            string message;
            switch (flow.LastQuestionAsked)
            {
                case ConversationFlow.Question.None:
                    await turnContext.SendActivityAsync("Let's get started. What is your name?");
                    flow.LastQuestionAsked = ConversationFlow.Question.Name;
                    break;
                case ConversationFlow.Question.Name:
                    if (ValidateName(input, out string name, out message))
                    {
                        profile.Name = name;
                        await turnContext.SendActivityAsync($"Hi {profile.Name}.");
                        await turnContext.SendActivityAsync("How old are you?");
                        flow.LastQuestionAsked = ConversationFlow.Question.Age;
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.");
                        break;
                    }

                case ConversationFlow.Question.Age:
                    if (ValidateAge(input, out int age, out message))
                    {
                        profile.Age = age;
                        await turnContext.SendActivityAsync($"I have your age as {profile.Age}.");
                        await turnContext.SendActivityAsync("When is your flight?");
                        flow.LastQuestionAsked = ConversationFlow.Question.Date;
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.");
                        break;
                    }

                case ConversationFlow.Question.Date:
                    if (ValidateDate(input, out string date, out message))
                    {
                        profile.Date = date;
                        await turnContext.SendActivityAsync($"Your cab ride to the airport is scheduled for {profile.Date}.");
                        await turnContext.SendActivityAsync($"Thanks for completing the booking {profile.Name}.");
                        await turnContext.SendActivityAsync($"Type anything to run the bot again.");
                        flow.LastQuestionAsked = ConversationFlow.Question.None;
                        profile = new UserProfile();
                        break;
                    }
                    else
                    {
                        await turnContext.SendActivityAsync(message ?? "I'm sorry, I didn't understand that.");
                        break;
                    }
            }
        }

        /// <summary>
        /// Validates name input.
        /// </summary>
        /// <param name="input">The user's input.</param>
        /// <param name="name">When the method returns, contains the normalized name, if validation succeeded.</param>
        /// <param name="message">When the method returns, contains a message with which to reprompt, if validation failed.</param>
        /// <returns>indicates whether validation succeeded.</returns>
        private static bool ValidateName(string input, out string name, out string message)
        {
            name = null;
            message = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                message = "Please enter a name that contains at least one character.";
            }
            else
            {
                name = input.Trim();
            }

            return message is null;
        }

        /// <summary>
        /// Validates age input.
        /// </summary>
        /// <param name="input">The user's input.</param>
        /// <param name="age">When the method returns, contains the normalized age, if validation succeeded.</param>
        /// <param name="message">When the method returns, contains a message with which to reprompt, if validation failed.</param>
        /// <returns>indicates whether validation succeeded.</returns>
        private static bool ValidateAge(string input, out int age, out string message)
        {
            age = 0;
            message = null;

            // Try to recognize the input as a number. This works for responses such as "twelve" as well as "12".
            try
            {
                // Attempt to convert the Recognizer result to an integer. This works for "a dozen", "twelve", "12", and so on.
                // The recognizer returns a list of potential recognition results, if any.
                List<ModelResult> results = NumberRecognizer.RecognizeNumber(input, Culture.English);
                foreach (ModelResult result in results)
                {
                    // The result resolution is a dictionary, where the "value" entry contains the processed string.
                    if (result.Resolution.TryGetValue("value", out object value))
                    {
                        age = Convert.ToInt32(value);
                        if (age >= 18 && age <= 120)
                        {
                            return true;
                        }
                    }
                }

                message = "Please enter an age between 18 and 120.";
            }
            catch
            {
                message = "I'm sorry, I could not interpret that as an age. Please enter an age between 18 and 120.";
            }

            return message is null;
        }

        /// <summary>
        /// Validates flight time input.
        /// </summary>
        /// <param name="input">The user's input.</param>
        /// <param name="date">When the method returns, contains the normalized date, if validation succeeded.</param>
        /// <param name="message">When the method returns, contains a message with which to reprompt, if validation failed.</param>
        /// <returns>indicates whether validation succeeded.</returns>
        private static bool ValidateDate(string input, out string date, out string message)
        {
            date = null;
            message = null;

            // Try to recognize the input as a date-time. This works for responses such as "11/14/2018", "9pm", "tomorrow", "Sunday at 5pm", and so on.
            // The recognizer returns a list of potential recognition results, if any.
            try
            {
                List<ModelResult> results = DateTimeRecognizer.RecognizeDateTime(input, Culture.English);

                // Check whether any of the recognized date-times are appropriate,
                // and if so, return the first appropriate date-time. We're checking for a value at least an hour in the future.
                DateTime earliest = DateTime.Now.AddHours(1.0);
                foreach (ModelResult result in results)
                {
                    // The result resolution is a dictionary, where the "values" entry contains the processed input.
                    List<Dictionary<string, string>> resolutions = result.Resolution["values"] as List<Dictionary<string, string>>;
                    foreach (Dictionary<string, string> resolution in resolutions)
                    {
                        // The processed input contains a "value" entry if it is a date-time value, or "start" and
                        // "end" entries if it is a date-time range.
                        if (resolution.TryGetValue("value", out string dateString)
                            || resolution.TryGetValue("start", out dateString))
                        {
                            if (DateTime.TryParse(dateString, out DateTime candidate)
                                && earliest < candidate)
                            {
                                date = candidate.ToShortDateString();
                                return true;
                            }
                        }
                    }
                }

                message = "I'm sorry, please enter a date at least an hour out.";
            }
            catch
            {
                message = "I'm sorry, I could not interpret that as an appropriate date. Please enter a date at least an hour out.";
            }

            return false;
        }
    }
}
