using LabWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace LabWeb.Auth;

public static class Features
{
    public const string Analytics = "Analytics";
    public const string SharedBoard = "SharedBoard";
}

[FilterAlias("Plan")]
public class PlanFilter(IHttpContextAccessor http) : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var currentUser = http.HttpContext?.RequestServices.GetService<CurrentUser>();
        return Task.FromResult(Allows(context, currentUser?.Community?.Plan));
    }

    internal static bool Allows(FeatureFilterEvaluationContext context, Plan? plan) =>
        plan is not null && (context.Parameters.GetSection("Plans").Get<Plan[]>() ?? []).Contains(plan.Value);
}

[FilterAlias("Plan")]
public class ExplicitPlanFilter : IContextualFeatureFilter<Plan>
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context, Plan plan) =>
        Task.FromResult(PlanFilter.Allows(context, plan));
}

public class RedirectToPremium : IDisabledFeaturesHandler
{
    public Task HandleDisabledFeatures(IEnumerable<string> features, ActionExecutingContext context)
    {
        context.Result = new RedirectToActionResult("Premium", "Community", null);
        return Task.CompletedTask;
    }
}
