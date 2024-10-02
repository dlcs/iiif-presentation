using System.Net;
using System.Runtime.InteropServices.JavaScript;
using API.Features.Storage.Helpers;
using API.Infrastructure.Requests;
using Core;
using Core.Helpers;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Models.API.General;

namespace API.Infrastructure;

public static class ControllerBaseX
{
    /// <summary>
    ///     Create an IActionResult from specified FetchEntityResult{T}.
    ///     This will be the model + 200 on success. Or an
    ///     error and appropriate status code if failed.
    /// </summary>
    /// <param name="controller">Current controllerBase object</param>
    /// <param name="entityResult">Result to transform</param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <returns>
    ///     ActionResult generated from FetchEntityResult
    /// </returns>
    public static IActionResult FetchResultToHttpResult<T>(this ControllerBase controller,
        FetchEntityResult<T> entityResult)
        where T : class
    {
        if (entityResult.Error)
        {
            return controller.PresentationProblem(detail: entityResult.ErrorMessage,
                statusCode: (int)HttpStatusCode.InternalServerError);
        }

        if (entityResult.EntityNotFound || entityResult.Entity == null) return controller.PresentationNotFound();

        return controller.Ok(entityResult.Entity);
    }

    /// <summary>
    /// Create an IActionResult from specified ModifyEntityResult{T}.
    /// This will be the model + 200/201 on success. Or an
    /// error and appropriate status code if failed.
    /// </summary>
    /// <param name="controller">Current controllerBase object</param>
    /// <param name="entityResult">Result to transform</param>
    /// <param name="instance">The value for <see cref="JSType.Error.Instance" />.</param>
    /// <param name="errorTitle">
    /// The value for <see cref="JSType.Error.Title" />. In some instances this will be prepended to the actual error name.
    /// e.g. errorTitle + ": Conflict"
    /// </param>
    /// <typeparam name="T">Type of entity being upserted</typeparam>
    /// <typeparam name="TEnum">An enum used for </typeparam>
    /// <returns>
    /// ActionResult generated from ModifyEntityResult
    /// </returns>
    public static IActionResult ModifyResultToHttpResult<T, TEnum>(this ControllerBase controller,
    ModifyEntityResult<T, TEnum> entityResult, 
    string? instance,
    string? errorTitle)
    where T : class 
    where TEnum : Enum =>
    entityResult.WriteResult switch
    {
        WriteResult.Updated => controller.Ok(entityResult.Entity),
        WriteResult.Created => controller.Created(controller.Request.GetDisplayUrl(), entityResult.Entity),
        WriteResult.NotFound => controller.PresentationNotFound(entityResult.Error),
        WriteResult.Error => controller.PresentationProblem(entityResult.Error, instance, 
            (int)HttpStatusCode.InternalServerError, errorTitle, controller.GetErrorType(entityResult.ErrorType)),
        WriteResult.BadRequest => controller.PresentationProblem(entityResult.Error, instance, 
            (int)HttpStatusCode.BadRequest, errorTitle, controller.GetErrorType(entityResult.ErrorType)),
        WriteResult.Conflict => controller.PresentationProblem(entityResult.Error, instance, (int)HttpStatusCode.Conflict, 
            $"{errorTitle}: Conflict", controller.GetErrorType(entityResult.ErrorType)),
        WriteResult.FailedValidation => controller.PresentationProblem(entityResult.Error, instance, (int)HttpStatusCode.BadRequest,
            $"{errorTitle}: Validation failed"),
        WriteResult.StorageLimitExceeded => controller.PresentationProblem(entityResult.Error, instance, (int)HttpStatusCode.InsufficientStorage,
            $"{errorTitle}: Storage limit exceeded"),
        WriteResult.PreConditionFailed => controller.PresentationProblem(entityResult.Error, instance, (int)HttpStatusCode.PreconditionFailed, 
            $"{errorTitle}: Pre-condition failed", controller.GetErrorType(entityResult.ErrorType)),
        _ => controller.PresentationProblem(entityResult.Error, instance, (int)HttpStatusCode.InternalServerError, errorTitle, controller.GetErrorType(entityResult.ErrorType)),
    };
    
    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult ValidationFailed(this ControllerBase controller, ValidationResult validationResult)
    {
        var message = string.Join(". ", validationResult.Errors.Select(s => s.ErrorMessage).Distinct());
        return controller.Problem(message, null, (int)HttpStatusCode.BadRequest, "Bad request");
    }
    
    /// <summary>
    /// Evaluates incoming orderBy and orderByDescending fields to get a suitable
    /// ordering field and its direction.
    /// </summary>
    public static string? GetOrderBy(this ControllerBase _, string? orderBy, string? orderByDescending,
        out bool descending)
    {
        string? orderByField = null;
        descending = false;
        if (orderBy.HasText() && OrderByHelper.AllowedOrderByFields.Contains(orderBy.ToLower()))
        {
            orderByField = $"orderBy={orderBy}";
        }
        else if (orderByDescending.HasText() && OrderByHelper.AllowedOrderByFields.Contains(orderByDescending.ToLower()))
        {
            orderByField = $"orderByDescending={orderByDescending}";
            descending = true;
        }

        return orderByField;
    }

    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response.
    /// </summary>
    /// <param name="statusCode">The value for <see cref="Error.Status" />.</param>
    /// <param name="detail">The value for <see cref="Error.Detail" />.</param>
    /// <param name="instance">The value for <see cref="Error.Instance" />.</param>
    /// <param name="title">The value for <see cref="Error.Title" />.</param>
    /// <param name="type">The value for <see cref="Error.Type" />.</param>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult PresentationProblem(
        this ControllerBase controller,
        string? detail = null,
        string? instance = null,
        int? statusCode = null,
        string? title = null,
        string? type = null)
    {
        var error = new Error
        {
            Detail = detail,
            Instance = instance ?? controller.Request.GetDisplayUrl(),
            Status = statusCode ?? 500,
            Title = title,
            ErrorTypeUri = type
        };

        return new ObjectResult(error)
        {
            StatusCode = error.Status
        };
    }

    public static string GetErrorType<TType>(this ControllerBase controller, TType type) =>
        $"{controller.Request.GetDisplayUrl()}/errors/{type?.GetType().Name}/{type}";
    
    
    /// <summary> 
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult PresentationNotFound(this ControllerBase controller, string? detail = null)
    {
        return controller.PresentationProblem(detail, null, (int)HttpStatusCode.NotFound, "Not Found");
    }
}