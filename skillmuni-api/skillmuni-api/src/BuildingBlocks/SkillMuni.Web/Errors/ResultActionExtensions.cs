using Microsoft.AspNetCore.Mvc;
using SkillMuni.Common.Results;

namespace SkillMuni.Web.Errors;

/// <summary>Maps a use-case <see cref="Result"/> to an HTTP response, so controllers stay one line.</summary>
public static class ResultActionExtensions
{
    /// <summary>204 on success.</summary>
    public static IActionResult ToActionResult(this Result result, ControllerBase controller) =>
        result.IsSuccess ? controller.NoContent() : ApiProblems.Result(controller.HttpContext, result.Error);

    /// <summary>200 with the value on success.</summary>
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller) =>
        result.IsSuccess ? controller.Ok(result.Value) : ApiProblems.Result(controller.HttpContext, result.Error);

    /// <summary>A custom success response (201, 202, cookies…).</summary>
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        Func<T, IActionResult> onSuccess) =>
        result.IsSuccess ? onSuccess(result.Value) : ApiProblems.Result(controller.HttpContext, result.Error);
}
