
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
        private static readonly ILogger<AlexaController> Logger = Mock.Of<ILogger<AlexaController>>();
        private static readonly IConfiguration Configuration = CreateConfiguration();

        private static IConfiguration CreateConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json");

            return configurationBuilder.Build();
        }

        private static readonly IParticipantsService ParticipantsService = CreateParticipantsService();
        private static readonly AlexaController Controller = new AlexaController(Logger, Configuration, ParticipantsService);
            
        [Fact]
        public void ReceiveRequest_IncorrectApplicationID()
        {
            // Arrange
            var skillsRequest = CreateSkillsRequest("", null);

            // Act
            var result = Controller.ReceiveRequest(skillsRequest);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }
        
        [Fact]
        public void LaunchRequest()
        {
            // Arrange
            var skillsRequest = CreateSkillsRequest(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID"), new LaunchRequest());

            // Act
            var result = Controller.ReceiveRequest(skillsRequest);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<SkillResponse>(objectResult.Value);
        }
        
        [Fact]
        public void IntentRequest_CreateEntry_RequireActivities()
        {
            // Arrange
            var skillsRequest = CreateSkillsRequest(Environment.GetEnvironmentVariable("ALEXA_SKILL_ID"), new IntentRequest
            {
                Type = "IntentRequest",
                Intent = new Intent
                {
                    Name = "CreateEntry",
                    Slots = new Dictionary<string, Slot>
                    {
                        {"mood", new Slot{ Value = "happy"}},
                        {"rating", new Slot{ Value = "10"}}
                    }
                }
            });

            // Act
            var result = Controller.ReceiveRequest(skillsRequest);

            // Assert
            var objectResult = Assert.IsType<OkObjectResult>(result);
            var skillsResponse = Assert.IsType<SkillResponse>(objectResult.Value);
            var plainTextOutputSpeech = Assert.IsType<PlainTextOutputSpeech>(skillsResponse.Response.OutputSpeech);

            Assert.Equal(Configuration["Responses:ActivitiesRequired"], plainTextOutputSpeech.Text);
        }
        
        private static IParticipantsService CreateParticipantsService()
        {
            var mockParticipantService = new Mock<IParticipantsService>();
            mockParticipantService.Setup(x => x.AddEntry(It.IsAny<string>(), It.IsAny<Entry>()));

            return mockParticipantService.Object;
        }

        private static SkillRequest CreateSkillsRequest(string applicationId, Request request)
        {
            
            var skillsRequest = new SkillRequest
            {
                Context = new Context()
                {
                    System = new AlexaSystem()
                    {
                        Application = new Application()
                        {
                            ApplicationId = applicationId
                        },
                    }
                },
                Request = request
            };

            return skillsRequest;
        }
    }
}