using MediaManagement.Models.Results;
using Microsoft.AspNetCore.Mvc;

namespace MediaManagement.Contracts;

public static class ResultExtensions
{
    public static IActionResult ToOk(this Result result, ControllerBase controller) =>
        result.IsSuccess ? controller.Ok() : Failure(result, controller);

    public static IActionResult ToOk<T>(this Result<T> result, ControllerBase controller) =>
        result.IsSuccess ? controller.Ok(result.Value) : Failure(result, controller);

    public static IActionResult ToCreatedAtAction<T>(
        this Result<T> result,
        ControllerBase controller,
        string actionName,
        object? routeValues
    ) =>
        result.IsSuccess
            ? controller.CreatedAtAction(actionName, routeValues, result.Value)
            : Failure(result, controller);

    public static IActionResult ToNoContent(this Result result, ControllerBase controller) =>
        result.IsSuccess ? controller.NoContent() : Failure(result, controller);

    private static ObjectResult Failure(Result result, ControllerBase controller) =>
        ApiProblems.ToActionResult(controller.HttpContext, result.ErrorType!.Value, result.Errors);
}
