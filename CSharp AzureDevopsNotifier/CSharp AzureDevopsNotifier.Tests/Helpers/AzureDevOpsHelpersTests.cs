using CSharp_AzureDevopsNotifier.Entities;
using CSharp_AzureDevopsNotifier.Helpers;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CSharp_AzureDevopsNotifier.Tests.Helpers
{
    internal class AzureDevOpsHelpersTests
    {
        // GetNewPullRequests method retrieves a list of pull requests
        [Test]
        public async Task Should_retrieve_list_of_pull_requests()
        {
            // Arrange
            var gitClientMock = new Mock<GitHttpClient>(It.IsAny<Uri>(), It.IsAny<VssCredentials>());
            var projectName = "project";
            var repositoryName = "repo";
            var expectedPullRequests = new List<GitPullRequest>
                {
                    new() { PullRequestId = 1, Title = "PR 1" },
                    new() { PullRequestId = 2, Title = "PR 2" }
                };
            gitClientMock.Setup(x => x.GetPullRequestsAsync(projectName, repositoryName, It.IsAny<GitPullRequestSearchCriteria>(), null, null, null, null, default))
                .ReturnsAsync(expectedPullRequests);

            // Act
            var result = await AzureDevOpsHelpers.GetNewPullRequestsAsync(gitClientMock.Object, projectName, repositoryName);

            // Assert
            Assert.That(result, Is.EqualTo(expectedPullRequests));
        }

        // GetWorkItems method retrieves a list of work items
        [Test]
        public async Task Should_retrieve_list_of_work_items()
        {
            // Arrange
            var workItemClientMock = new Mock<WorkItemTrackingHttpClient>(It.IsAny<Uri>(), It.IsAny<VssCredentials>());
            var filters = new List<string> { "System.WorkItemType = 'Bug'" };
            var expectedWorkItems = new List<WorkItem>
                {
                    new() { Id = 1, Fields = new Dictionary<string, object> { { "System.Title", "Bug 1" } } },
                    new() { Id = 2, Fields = new Dictionary<string, object> { { "System.Title", "Bug 2" } } }
                };
            var queryResult = new WorkItemQueryResult { WorkItems = expectedWorkItems.Select(w => new WorkItemReference { Id = w.Id ?? 0 }).ToList() };
            workItemClientMock.Setup(x => x.QueryByWiqlAsync(It.IsAny<Wiql>(), null, null, null, default)).ReturnsAsync(queryResult);
            workItemClientMock.Setup(x => x.GetWorkItemsAsync(It.IsAny<IEnumerable<int>>(), null, null, null, null, null, default)).ReturnsAsync(expectedWorkItems);

            // Act
            var result = await AzureDevOpsHelpers.GetWorkItemsAsync(workItemClientMock.Object, filters);

            // Assert
            Assert.That(result, Is.EqualTo(expectedWorkItems));
        }

        [Test]
        public void Test_display_new_PR_toast_notification()
        {
            // Arrange
            var items = new List<GitPullRequest>
                {
                    new() { PullRequestId = 1, Title = "PR 1" },
                    new() { PullRequestId = 2, Title = "PR 2" }
                };
            var displayedItemIds = new List<int> { 1 };
            var settings = new AzureDevOpsSettings();
            var query = new AzureDevOpsQuery();

            // Act
            AzureDevOpsHelpers.DisplayNewItems(items, displayedItemIds, settings, query);

            // Assert
            Assert.Pass();
        }

        [Test]
        public void Test_display_new_work_items_toast_notification()
        {
            // Arrange
            var items = new List<WorkItem>
                {
                    new() { Id = 1, Fields = new Dictionary<string, object> { { "System.Title", "Bug 1" } } },
                    new() { Id = 2, Fields = new Dictionary<string, object> { { "System.Title", "Bug 2" } }  }
                };
            var displayedItemIds = new List<int> { 1 };
            var settings = new AzureDevOpsSettings();
            var query = new AzureDevOpsQuery();

            // Act
            AzureDevOpsHelpers.DisplayNewItems(items, displayedItemIds, settings, query);

            // Assert
            Assert.Pass();
        }
    }
}
