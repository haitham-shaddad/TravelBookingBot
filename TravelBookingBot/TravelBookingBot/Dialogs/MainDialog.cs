// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio CoreBot v4.6.2

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using AdaptiveCards.Templating;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Recognizers.Text.DataTypes.TimexExpression;
using Newtonsoft.Json;
using TravelBookingBot.Cards;
using TravelBookingBot.CognitiveModels;

namespace TravelBookingBot.Dialogs
{
    public class MainDialog : ComponentDialog
    {
        private readonly FlightBookingRecognizer _luisRecognizer;
        protected readonly ILogger Logger;

        // Dependency injection uses this constructor to instantiate MainDialog
        public MainDialog(FlightBookingRecognizer luisRecognizer, BookingDialog bookingDialog, ILogger<MainDialog> logger)
            : base(nameof(MainDialog))
        {
            _luisRecognizer = luisRecognizer;
            Logger = logger;

            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(bookingDialog);
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                IntroStepAsync,
                ActStepAsync,
                FinalStepAsync,
            }));

            // The initial child Dialog to run.
            InitialDialogId = nameof(WaterfallDialog);
        }

        private async Task<DialogTurnResult> IntroStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                await stepContext.Context.SendActivityAsync(
                    MessageFactory.Text("NOTE: LUIS is not configured. To enable all capabilities, add 'LuisAppId', 'LuisAPIKey' and 'LuisAPIHostName' to the appsettings.json file.", inputHint: InputHints.IgnoringInput), cancellationToken);

                return await stepContext.NextAsync(null, cancellationToken);
            }

            // Use the text provided in FinalStepAsync or the default if it is the first time.
            var messageText = stepContext.Options?.ToString() ?? "What can I help you with today?\nSay something like \"Book a flight from Paris to Berlin on March 22, 2020\"";
            var promptMessage = MessageFactory.Text(messageText, messageText, InputHints.ExpectingInput);
            return await stepContext.PromptAsync(nameof(TextPrompt), new PromptOptions { Prompt = promptMessage }, cancellationToken);
        }

        private async Task<DialogTurnResult> ActStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            if (!_luisRecognizer.IsConfigured)
            {
                // LUIS is not configured, we just run the BookingDialog path with an empty BookingDetailsInstance.
                return await stepContext.BeginDialogAsync(nameof(BookingDialog), new BookingDetails(), cancellationToken);
            }

            // Call LUIS and gather any potential booking details. (Note the TurnContext has the response to the prompt.)
            var luisResult = await _luisRecognizer.RecognizeAsync<FlightBooking>(stepContext.Context, cancellationToken);
            switch (luisResult.TopIntent().intent)
            {
                case FlightBooking.Intent.BookFlight:
                    await ShowWarningForUnsupportedCities(stepContext.Context, luisResult, cancellationToken);

                    // Initialize BookingDetails with any entities we may have found in the response.
                    var bookingDetails = new BookingDetails()
                    {
                        // Get destination and origin from the composite entities arrays.
                        Destination = luisResult.ToEntities.Airport,
                        Origin = luisResult.FromEntities.Airport,
                        TravelDate = luisResult.TravelDate,
                    };

                    // Run the BookingDialog giving it whatever details we have from the LUIS call, it will fill out the remainder.
                    return await stepContext.BeginDialogAsync(nameof(BookingDialog), bookingDetails, cancellationToken);

                case FlightBooking.Intent.GetWeather:
                    // We haven't implemented the GetWeatherDialog so we just display a TODO message.
                    var getWeatherMessageText = "TODO: get weather flow here";
                    var getWeatherMessage = MessageFactory.Text(getWeatherMessageText, getWeatherMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(getWeatherMessage, cancellationToken);
                    break;

                default:
                    // Catch all for unhandled intents
                    var didntUnderstandMessageText = $"Sorry, I didn't get that. Please try asking in a different way (intent was {luisResult.TopIntent().intent})";
                    var didntUnderstandMessage = MessageFactory.Text(didntUnderstandMessageText, didntUnderstandMessageText, InputHints.IgnoringInput);
                    await stepContext.Context.SendActivityAsync(didntUnderstandMessage, cancellationToken);
                    break;
            }

            return await stepContext.NextAsync(null, cancellationToken);
        }

        // Shows a warning if the requested From or To cities are recognized as entities but they are not in the Airport entity list.
        // In some cases LUIS will recognize the From and To composite entities as a valid cities but the From and To Airport values
        // will be empty if those entity values can't be mapped to a canonical item in the Airport.
        private static async Task ShowWarningForUnsupportedCities(ITurnContext context, FlightBooking luisResult, CancellationToken cancellationToken)
        {
            var unsupportedCities = new List<string>();

            var fromEntities = luisResult.FromEntities;
            if (!string.IsNullOrEmpty(fromEntities.From) && string.IsNullOrEmpty(fromEntities.Airport))
            {
                unsupportedCities.Add(fromEntities.From);
            }

            var toEntities = luisResult.ToEntities;
            if (!string.IsNullOrEmpty(toEntities.To) && string.IsNullOrEmpty(toEntities.Airport))
            {
                unsupportedCities.Add(toEntities.To);
            }

            if (unsupportedCities.Any())
            {
                var messageText = $"Sorry but the following airports are not supported: {string.Join(',', unsupportedCities)}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);
                await context.SendActivityAsync(message, cancellationToken);
            }
        }

        private async Task<DialogTurnResult> FinalStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            // If the child dialog ("BookingDialog") was cancelled, the user failed to confirm or if the intent wasn't BookFlight
            // the Result here will be null.
            if (stepContext.Result is BookingDetails result)
            {
                // Now we have all the booking details call the booking service.

                // If the call to the booking service was successful tell the user.

                var timeProperty = new TimexProperty(result.TravelDate);
                var travelDateMsg = timeProperty.ToNaturalLanguage(DateTime.Now);
                var messageText = $"I have you booked to {result.Destination} from {result.Origin} on {travelDateMsg}";
                var message = MessageFactory.Text(messageText, messageText, InputHints.IgnoringInput);

                var model = new FlightitineraryModel
                {
                    Places = new Cards.Place[] {
                        new Cards.Place { Name = result.Origin ,Code=result.Origin},
                        new Cards.Place { Name = result.Destination ,Code=result.Destination}
                    },
                    Segments = new Segment[]
                    {
                        new Segment{Id = 1, DepartureDateTime = DateTime.Parse(result.TravelDate)},
                        new Segment{Id = 2,DepartureDateTime = DateTime.Parse(result.TravelDate)}
                    },
                    Query = new Query
                    {
                        DestinationPlace = result.Destination,
                        OriginPlace = result.Origin,
                        InboundDate = result.TravelDate,
                        OutboundDate = result.TravelDate
                    },
                    BookingOptions = new Bookingoption[]
                    {
                        new Bookingoption
                        {
                            BookingItems = new Bookingitem[]
                            {
                                new Bookingitem
                                {
                                    Price = new Random().Next(),SegmentIds =new int[]{1,2}
                                }
                            }
                        }
                    }
                };

                var confirmationCard = CreateBookingConfimrationCardAttachment(model);
                message.Attachments.Add(confirmationCard);

                await stepContext.Context.SendActivityAsync(message, cancellationToken);
            }

            // Restart the main dialog with a different message the second time around
            var promptMessage = "What else can I do for you?";
            return await stepContext.ReplaceDialogAsync(InitialDialogId, promptMessage, cancellationToken);
        }


        private Attachment CreateBookingConfimrationCardAttachment(FlightitineraryModel data)
        {
            try
            {
                var cardResourcePath = "TravelBookingBot.Cards.flightCard.json";

                using (var stream = GetType().Assembly.GetManifestResourceStream(cardResourcePath))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        var adaptiveCardTempalte = reader.ReadToEnd();

                        var dataJson = JsonConvert.SerializeObject(data);
                        //dataJson = @"{
                        //                ""Segments"": [
                        //                {
                        //                    ""Id"": 1,
                        //                    ""OriginStation"": 11235,
                        //                    ""DestinationStation"": 13554,
                        //                    ""DepartureDateTime"": ""2017-05-30T19:25:00Z"",
                        //                    ""ArrivalDateTime"": ""2017-05-30T20:55:00Z"",
                        //                    ""Carrier"": 881,
                        //                    ""OperatingCarrier"": 881,
                        //                    ""Duration"": 90,
                        //                    ""FlightNumber"": ""1463"",
                        //                    ""JourneyMode"": ""Flight"",
                        //                    ""Directionality"": ""Outbound""
                        //                },
                        //                {
                        //                    ""Id"": 2,
                        //                    ""OriginStation"": 13554,
                        //                    ""DestinationStation"": 11235,
                        //                    ""DepartureDateTime"": ""2017-06-02T19:25:00Z"",
                        //                    ""ArrivalDateTime"": ""2017-06-02T20:55:00Z"",
                        //                    ""Carrier"": 881,
                        //                    ""OperatingCarrier"": 881,
                        //                    ""Duration"": 90,
                        //                    ""FlightNumber"": ""1463"",
                        //                    ""JourneyMode"": ""Flight"",
                        //                    ""Directionality"": ""Inbound""
                        //                }
                        //                ],
                        //                ""BookingOptions"": [
                        //                {
                        //                    ""BookingItems"": [
                        //                    {
                        //                        ""AgentID"": 4499211,
                        //                        ""Status"": ""Current"",
                        //                        ""Price"": 4032.54,
                        //                        ""Deeplink"": ""http://partners.api.skyscanner.net/apiservices/deeplink/v2?_cje=jzj5DawL5[...]"",
                        //                        ""SegmentIds"": [
                        //                        1,
                        //                        2
                        //                        ]
                        //                }
                        //                    ]
                        //                }
                        //                ],
                        //                ""Places"": [
                        //                {
                        //                    ""Id"": 13554,
                        //                    ""ParentId"": 4698,
                        //                    ""Code"": ""SFO"",
                        //                    ""Type"": ""Airport"",
                        //                    ""Name"": ""San Francisco""
                        //                },
                        //                {
                        //                    ""Id"": 13558,
                        //                    ""ParentId"": 5796,
                        //                    ""Code"": ""AMS"",
                        //                    ""Type"": ""Airport"",
                        //                    ""Name"": ""Amsterdam""
                        //                }
                        //                ],
                        //                ""Carriers"": [
                        //                {
                        //                    ""Id"": 881,
                        //                    ""Code"": ""BA"",
                        //                    ""Name"": ""British Airways"",
                        //                    ""ImageUrl"": ""http://s1.apideeplink.com/images/airlines/BA.png""
                        //                }
                        //                ],
                        //                ""Query"": {
                        //                ""Country"": ""GB"",
                        //                ""Currency"": ""GBP"",
                        //                ""Locale"": ""en-gb"",
                        //                ""Adults"": 3,
                        //                ""Children"": 0,
                        //                ""Infants"": 0,
                        //                ""OriginPlace"": ""2343"",
                        //                ""DestinationPlace"": ""13554"",
                        //                ""OutboundDate"": ""2017-05-30"",
                        //                ""InboundDate"": ""2017-06-02"",
                        //                ""LocationSchema"": ""Default"",
                        //                ""CabinClass"": ""Economy"",
                        //                ""GroupPricing"": false
                        //                }
                        //                }";
                        var transformer = new AdaptiveTransformer();
                        var cardJson = transformer.Transform(adaptiveCardTempalte, dataJson);

                        return new Attachment()
                        {
                            ContentType = AdaptiveCards.AdaptiveCard.ContentType,// "application/vnd.microsoft.card.adaptive",
                            Content = cardJson,
                        };
                    }
                }
            }
            catch (Exception ex)
            {

                throw;
            }
        }

    }
}
