using Microsoft.AspNetCore.Routing;
using PrIMA.Api.Features.Issues.AcknowledgeIssue;
using PrIMA.Api.Features.Issues.AddNote;
using PrIMA.Api.Features.Issues.AssignIssue;
using PrIMA.Api.Features.Issues.DismissIssue;
using PrIMA.Api.Features.Issues.GetIssueById;
using PrIMA.Api.Features.Issues.RecordManualVerification;
using PrIMA.Api.Features.Issues.RecordRemediation;
using PrIMA.Api.Features.Issues.ResolveIssue;
using PrIMA.Api.Features.Issues.StartInvestigation;
using PrIMA.Api.Features.Issues.TakeIt;
using PrIMA.Api.Features.Issues.TriggerVerificationRun;

namespace PrIMA.Api.Features.Issues;

public static class IssueLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapIssueLifecycleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGetIssueByIdEndpoint();
        endpoints.MapTakeItEndpoint();
        endpoints.MapAssignIssueEndpoint();
        endpoints.MapAcknowledgeIssueEndpoint();
        endpoints.MapStartInvestigationEndpoint();
        endpoints.MapAddNoteEndpoint();
        endpoints.MapRecordRemediationEndpoint();
        endpoints.MapRecordManualVerificationEndpoint();
        endpoints.MapTriggerVerificationRunEndpoint();
        endpoints.MapResolveIssueEndpoint();
        endpoints.MapDismissIssueEndpoint();

        return endpoints;
    }
}
