using System.Net;
using System.Runtime.InteropServices.JavaScript;
using API.Features.Storage.Helpers;
using API.Infrastructure.Http;
using API.Infrastructure.Requests;
using Core;
using Core.Helpers;
using FluentValidation.Results;
using IIIF;
using IIIF.Presentation;
using IIIF.Presentation.V3;
using IIIF.Serialisation;
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
        where T : JsonLdBase
        where TEnum : Enum =>
        entityResult.WriteResult switch
        {
            WriteResult.Updated => controller.PresentationContent(entityResult.Entity, etag: entityResult.ETag),
            WriteResult.Accepted => controller.PresentationWithLocationHeader(controller.Request.GetDisplayUrl(),
                entityResult.Entity, (int)HttpStatusCode.Accepted, entityResult.ETag),
            WriteResult.Created => controller.PresentationWithLocationHeader(controller.Request.GetDisplayUrl(),
                entityResult.Entity, (int)HttpStatusCode.Created, entityResult.ETag),
            WriteResult.NotFound => controller.PresentationNotFound(entityResult.Error),
            WriteResult.Error => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.InternalServerError, errorTitle, controller.GetErrorType(entityResult.ErrorType)),
            WriteResult.BadRequest => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.BadRequest, errorTitle, controller.GetErrorType(entityResult.ErrorType)),
            WriteResult.Conflict => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.Conflict,
                $"{errorTitle}: Conflict", controller.GetErrorType(entityResult.ErrorType)),
            WriteResult.FailedValidation => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.BadRequest,
                $"{errorTitle}: Validation failed", controller.GetErrorType(ModifyCollectionType.ValidationFailed)),
            WriteResult.Forbidden => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.Forbidden,
                $"{errorTitle}: Forbidden"),
            WriteResult.StorageLimitExceeded => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.InsufficientStorage,
                $"{errorTitle}: Storage limit exceeded"),
            WriteResult.PreConditionFailed => controller.PresentationProblem(entityResult.Error, instance,
                (int)HttpStatusCode.PreconditionFailed,
                $"{errorTitle}: Pre-condition failed", controller.GetErrorType(entityResult.ErrorType)),
            _ => controller.PresentationProblem(entityResult.Error, instance, (int)HttpStatusCode.InternalServerError,
                errorTitle, controller.GetErrorType(entityResult.ErrorType)),
        };

    /// <summary>
    /// Creates an <see cref="ObjectResult"/> that produces a <see cref="Error"/> response with 404 status code.
    /// </summary>
    /// <returns>The created <see cref="ObjectResult"/> for the response.</returns>
    public static ObjectResult ValidationFailed(this ControllerBase controller, ValidationResult validationResult)
    {
        var message = string.Join(". ", validationResult.Errors.Select(s => s.ErrorMessage).Distinct());
        return controller.PresentationProblem(message, null, (int)HttpStatusCode.BadRequest, "Bad request",
            GetErrorType(controller, ModifyCollectionType.ValidationFailed));
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
            orderByField = orderBy;
        }
        else if (orderByDescending.HasText() &&
                 OrderByHelper.AllowedOrderByFields.Contains(orderByDescending.ToLower()))
        {
            orderByField = orderByDescending;
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
    /// <param name="type">The value for <see cref="Type" />.</param>
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

    /// <summary>
    /// Creates a result with serialised <see cref="JsonLdBase"/> body, correct IIIF ContentType and specified
    /// statuscode 
    /// </summary>
    public static ContentResult PresentationContent(this ControllerBase _, JsonLdBase descriptionResource,
        int statusCode = 200, Guid? etag = null)
    {
        var result = etag.HasValue ? new CacheableContentResult { ETag = etag.Value } : new ContentResult();

        result.Content = descriptionResource.AsJson();
        result.ContentType = ContentTypes.V3;
        result.StatusCode = statusCode;

        return result;
    }

    /// <summary>
    /// Create an <see cref="ObjectResult"/> that produced a 403 response
    /// </summary>
    public static ObjectResult Forbidden(this ControllerBase controller)
        => controller.PresentationProblem(statusCode: (int)HttpStatusCode.Forbidden);

    /// <summary>
    /// Creates a result with serialised <see cref="JsonLdBase"/> body, specified status code and Location header set
    /// </summary>
    public static ActionResult PresentationWithLocationHeader(this ControllerBase controller, string? uri,
        JsonLdBase descriptionResource, int statusCode, Guid? etag)
    {
        if (descriptionResource is ResourceBase { Id: { Length: > 0 } id }) uri = id;

        controller.Response.Headers.Location = uri;

        return PresentationContent(controller, descriptionResource, statusCode, etag);
    }
}
