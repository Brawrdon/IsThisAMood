using System;
using System.Collections.Generic;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using IsThisAMood.Controllers;
using IsThisAMood.Models.Database;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsThisAMoodTests
{
    public class AlexaControllerTests
    {
   
        private static readonly string IntentRequest = "IntentRequest";

        private static readonly ILogger<AlexaController> Logger = Mock.Of<ILogger<AlexaController>>();
        private static readonly IConfiguration Configuration = CreateConfiguration();
        private static readonly IParticipantsService ParticipantsService = CreateParticipantsService();
        private static readonly Mock<IParticipantsAuthenticationService> ParticipantsAuthenticationService = new Mock<IParticipantsAuthenticationService>();

        
        private readonly AlexaController _controller;

        public AlexaControllerTests()
        {
            _controller = new AlexaController(Logger, Configuration, ParticipantsService, ParticipantsAuthenticationService.Object);
        }

        private static IConfiguration CreateConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json");

            return configurationBuilder.Build();
        }

        private static IParticipantsService CreateParticipantsService()
        {
            var mockParticipantService = new Mock<IParticipantsService>();
            mockParticipantService.Setup(x => x.AddEntry(It.IsAny<string>(), It.IsAny<Entry>()));

            return mockParticipantService.Object;
        }

        private static SkillRequest CreateSkillsRequest(string applicationId, string sessionId, Request request)
        {
            var skillsRequest = new SkillRequest
            {
                Session = new Session()
                {
                    SessionId = sessionId
                },
                Context = new Context
                {
                    System = new AlexaSystem
                    {
                        Application = new Application
                        {
                            ApplicationId = applicationId
                        },
                    }
                },
                Request = request
            };

            return skillsRequest;
        }

        [Fact]
        public void IntentRequest_AddActivities()
        {
            // Arrange
            const string sessionId = "session-1";
            var activityIntentRequestOne = new IntentRequest
            {
                Type = IntentRequest,
                Intent = new Intent
                {
                    Name = "AddActivity",
                    Slots = new Dictionary<string, Slot>
                    {
                        {"activity", new Slot {Value = "friends"}},
                    }
                }
            };

            var activityIntentRequestTwo = new IntentRequest
            {
                Type = IntentRequest,
                Intent = new Intent
                {
                    Name = "AddActivity",
                    Slots = new Dictionary<string, Slot>
                    {
                        {"activity", new Slot {Value = "family"}},
                    }
                }
            };
            
            // Act
            var activityRequest = CreateSkillsRequest(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID"), sessionId,
                activityIntentRequestOne);
            var activityRequestResult = _controller.ReceiveRequest(activityRequest);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(activityRequestResult);
            var skillsResponse = Assert.IsType<SkillResponse>(objectResult.Value);
            var plainTextOutputSpeech = Assert.IsType<PlainTextOutputSpeech>(skillsResponse.Response.OutputSpeech);
            Assert.Equal(Configuration["Responses:ActivityRequest"], plainTextOutputSpeech.Text);

            // Act
            activityRequest = CreateSkillsRequest(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID"), sessionId,
                activityIntentRequestTwo);
            activityRequestResult = _controller.ReceiveRequest(activityRequest);

            // Assert
            objectResult = Assert.IsType<OkObjectResult>(activityRequestResult);
            skillsResponse = Assert.IsType<SkillResponse>(objectResult.Value);
            plainTextOutputSpeech = Assert.IsType<PlainTextOutputSpeech>(skillsResponse.Response.OutputSpeech);
            Assert.Equal(Configuration["Responses:ActivityRequest"], plainTextOutputSpeech.Text);
        }

        [Fact]
        public void IntentRequest_CreateEntry()
        {
            // Arrange
            var skillsRequest = CreateSkillsRequest(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID"), "",
                new IntentRequest
                {
                    Type = IntentRequest,
                    Intent = new Intent
                    {
                        Name = "CreateEntry",
                        Slots = new Dictionary<string, Slot>
                        {
                            {"mood", new Slot {Value = "happy"}},
                            {"rating", new Slot {Value = "10"}}
                        }
                    }
                });

            // Act
            var result = _controller.ReceiveRequest(skillsRequest);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result);
            var skillsResponse = Assert.IsType<SkillResponse>(objectResult.Value);
            var plainTextOutputSpeech = Assert.IsType<PlainTextOutputSpeech>(skillsResponse.Response.OutputSpeech);

            Assert.Equal(Configuration["Responses:FirstActivityRequest"], plainTextOutputSpeech.Text);
        }

        [Fact]
        public void LaunchRequest()
        {
            // Arrange
            var skillsRequest = CreateSkillsRequest(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID"), "",
                new LaunchRequest());

            // Act
            var result = _controller.ReceiveRequest(skillsRequest);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<SkillResponse>(objectResult.Value);
        }

        [Fact]
        public void ReceiveRequest_IncorrectApplicationID()
        {
            // Arrange
            var skillsRequest = CreateSkillsRequest("", "", null);

            // Act
            var result = _controller.ReceiveRequest(skillsRequest);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}