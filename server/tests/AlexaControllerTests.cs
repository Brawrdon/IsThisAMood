using System;
using System.Threading.Tasks;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Castle.Core.Logging;
using IsThisAMood.Controllers;
using IsThisAMood.Models.Database;
using IsThisAMood.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ILogger = Castle.Core.Logging.ILogger;

namespace IsThisAMoodTests
{
    public class AlexaControllerTests
    {
        [Fact]
        public void ReceiveRequest_IncorrectApplicationID()
        {
            // Arrange
            var logger = Mock.Of<ILogger<AlexaController>>();
            var configuration = Mock.Of<IConfiguration>();
            var participantsService = GenerateParticipantsService();
            var controller = new AlexaController(logger, configuration, participantsService);

            var skillsRequest = GenerateSkillsRequest("", null);

            // Act
            var result = controller.ReceiveRequest(skillsRequest);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        private static IParticipantsService GenerateParticipantsService()
        {
            var mockParticipantService = new Mock<IParticipantsService>();
            mockParticipantService.Setup(x => x.AddEntry(It.IsAny<string>(), It.IsAny<Entry>()));

            return mockParticipantService.Object;
        }

        private static SkillRequest GenerateSkillsRequest(string applicationId, Request request)
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