using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;
using SkillMuni.Common.Results;
using SkillMuni.Web.Errors;

namespace SkillMuni.Web.Validation;

/// <summary>
/// Runs the FluentValidation validator registered for each action argument. A request that is valid JSON but
/// breaks a rule gets 422 with <c>errors.code = ["validation"]</c> and one entry per field (CLAUDE.md §4).
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            if (result.IsValid)
            {
                continue;
            }

            var fields = result.Errors
                .GroupBy(e => FieldName(e.PropertyName))
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
            context.Result = ApiProblems.Result(
                context.HttpContext,
                Error.Validation("validation", "Check the highlighted fields.", fields));
            return;
        }

        await next();
    }

    public static string FieldName(string propertyPath) =>
        string.Join('.', propertyPath.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
