using LabWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.Mvc;

namespace LabWeb.Auth;

public static class Features
{
    public const string Analytics = "Analytics";
}

[FilterAlias("Plan")]
public class PlanFilter(IHttpContextAccessor http) : IFeatureFilter
{
    public Task<bool> EvaluateAsync(FeatureFilterEvaluationContext context)
    {
        var plans = context.Parameters.GetSection("Plans").Get<Plan[]>() ?? [];
        var currentUser = http.HttpContext?.RequestServices.GetService<CurrentUser>();
        var plan = currentUser?.Community?.Plan;
        return Task.FromResult(plan is not null && plans.Contains(plan.Value));
    }
}

public class RedirectToPremium : IDisabledFeaturesHandler
{
    public Task HandleDisabledFeatures(IEnumerable<string> features, ActionExecutingContext context)
    {
        context.Result = new RedirectToActionResult("Premium", "Community", null);
        return Task.CompletedTask;
    }
}
