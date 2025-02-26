// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.

using EdFi.Ods.AdminApi.Infrastructure.ClaimSetEditor;
using FluentValidation;

namespace EdFi.Ods.AdminApi.Features.ClaimSets;

public class ResourceClaimValidator
{
    private static List<string>? _duplicateResources;
    public ResourceClaimValidator()
    {
        _duplicateResources = new List<string>();
    }

    public void Validate<T>(Lookup<string, ResourceClaim> dbResourceClaims, List<string> dbActions,
        List<string?> dbAuthStrategies, ClaimSetResourceClaimModel resourceClaim, List<ChildrenClaimSetResource> existingResourceClaims,
        ValidationContext<T> context, string? claimSetName)
    {
        context.MessageFormatter.AppendArgument("ClaimSetName", claimSetName);
        context.MessageFormatter.AppendArgument("ResourceClaimName", resourceClaim.Name);

        var propertyName = "ResourceClaims";
        ValidateDuplicateResourceClaim(resourceClaim, existingResourceClaims, context, propertyName);

        ValidateCRUD(resourceClaim.Actions, dbActions, context, propertyName);

        var resources = dbResourceClaims[resourceClaim.Name!.ToLower()].ToList();
        ValidateIfExist(context, propertyName, resources);
        ValidateAuthStrategies(dbAuthStrategies, resourceClaim, context, propertyName);
        ValidateAuthStrategiesOverride(dbAuthStrategies, resourceClaim, context, propertyName);
        ValidateChildren(dbResourceClaims, dbActions, dbAuthStrategies, resourceClaim, context, claimSetName, propertyName, resources);
    }

    public void Validate<T>(Lookup<int, ResourceClaim> dbResourceClaims, List<string> dbActions, IResourceClaimOnClaimSetRequest editResourceClaimOnClaimSetRequest,
        ValidationContext<T> context, string? claimSetName)
    {
        context.MessageFormatter.AppendArgument("ClaimSetName", claimSetName);
        context.MessageFormatter.AppendArgument("ResourceClaimName", editResourceClaimOnClaimSetRequest.ResourceClaimId);

        var propertyName = "ResourceClaimActions";
        var resources = dbResourceClaims[editResourceClaimOnClaimSetRequest.ResourceClaimId].ToList();
        ValidateIfExist(context, propertyName, resources);
        ValidateCRUD(editResourceClaimOnClaimSetRequest.ResourceClaimActions, dbActions, context, propertyName);
    }

    private static void ValidateIfExist<T>(ValidationContext<T> context, string propertyName, List<ResourceClaim> resources)
    {
        if (!resources.Any())
        {
            context.AddFailure(propertyName, "This Claim Set contains a resource which is not in the system. Claimset Name: '{ClaimSetName}' Resource: '{ResourceClaimName}'.");
        }
    }

    private static void ValidateDuplicateResourceClaim<T>(ClaimSetResourceClaimModel resourceClaim, List<ChildrenClaimSetResource> existingResourceClaims, ValidationContext<T> context, string propertyName)
    {
        if (existingResourceClaims.Count(x => x.Name == resourceClaim.Name) > 1)
        {
            if (_duplicateResources != null && resourceClaim.Name != null && !_duplicateResources.Contains(resourceClaim.Name))
            {
                _duplicateResources.Add(resourceClaim.Name);
                context.AddFailure(propertyName, "Only unique resource claims can be added. The following is a duplicate resource: '{ResourceClaimName}'.");
            }
        }
    }

    private void ValidateChildren<T>(Lookup<string, ResourceClaim> dbResourceClaims, List<string> dbActions,
        List<string?> dbAuthStrategies, ClaimSetResourceClaimModel resourceClaim,
        ValidationContext<T> context, string? claimSetName, string propertyName, List<ResourceClaim> resources)
    {
        if (resourceClaim.Children.Any())
        {
            foreach (var child in resourceClaim.Children)
            {
                var childResources = dbResourceClaims[child.Name!.ToLower()].ToList();
                if (childResources.Any())
                {
                    foreach (var childResource in childResources)
                    {
                        context.MessageFormatter.AppendArgument("ChildResource", childResource.Name);
                        if (childResource.ParentId == 0)
                        {
                            context.AddFailure(propertyName, "'{ChildResource}' can not be added as a child resource.");
                        }

                        else if (!resources.Where(x => x is not null).Select(x => x.Id).Contains(childResource.ParentId))
                        {
                            context.MessageFormatter.AppendArgument("CorrectParentResource", childResource.ParentName);
                            context.AddFailure(propertyName, "Child resource: '{ChildResource}' added to the wrong parent resource. Correct parent resource is: '{CorrectParentResource}'.");
                        }
                    }
                }
                Validate(dbResourceClaims, dbActions, dbAuthStrategies, child, resourceClaim.Children, context, claimSetName);
            }
        }
    }

    private static void ValidateAuthStrategiesOverride<T>(List<string?> dbAuthStrategies,
        ClaimSetResourceClaimModel resourceClaim, ValidationContext<T> context, string propertyName)
    {
        if (resourceClaim.AuthorizationStrategyOverridesForCRUD != null && resourceClaim.AuthorizationStrategyOverridesForCRUD.Any())
        {
            foreach (var authStrategyOverrideWithAction in resourceClaim.AuthorizationStrategyOverridesForCRUD)
            {
                if (authStrategyOverrideWithAction?.AuthorizationStrategies != null)
                foreach (var authStrategyOverride in authStrategyOverrideWithAction.AuthorizationStrategies)
                {
                    if (authStrategyOverride?.AuthStrategyName != null && !dbAuthStrategies.Contains(authStrategyOverride.AuthStrategyName))
                    {
                        context.MessageFormatter.AppendArgument("AuthStrategyName", authStrategyOverride.AuthStrategyName);
                        context.AddFailure(propertyName, "This resource claim contains an authorization strategy which is not in the system. Claimset Name: '{ClaimSetName}' Resource name: '{ResourceClaimName}' Authorization strategy: '{AuthStrategyName}'.");
                    }
                }            
            }
        }
    }

    private static void ValidateAuthStrategies<T>(List<string?> dbAuthStrategies,
        ClaimSetResourceClaimModel resourceClaim, ValidationContext<T> context, string propertyName)
    {
        if (resourceClaim.DefaultAuthorizationStrategiesForCRUD != null && resourceClaim.DefaultAuthorizationStrategiesForCRUD.Any())
        {
            foreach (var defaultASWithAction in resourceClaim.DefaultAuthorizationStrategiesForCRUD)
            {
                if(defaultASWithAction?.AuthorizationStrategies != null)
                foreach(var defaultAS in defaultASWithAction.AuthorizationStrategies)
                {
                    if (defaultAS?.AuthStrategyName != null && !dbAuthStrategies.Contains(defaultAS.AuthStrategyName))
                    {
                        context.MessageFormatter.AppendArgument("AuthStrategyName", defaultAS.AuthStrategyName);
                        context.AddFailure(propertyName, "This resource claim contains an authorization strategy which is not in the system. Claimset Name: '{ClaimSetName}' Resource name: '{ResourceClaimName}' Authorization strategy: '{AuthStrategyName}'.");
                    }
                }               
            }
        }
    }

    private static void ValidateCRUD<T>(List<ResourceClaimAction>? resourceClaimActions,
        List<string> dbActions, ValidationContext<T> context, string propertyName)
    {
        if (resourceClaimActions != null && resourceClaimActions.Any())
        {
            var atleastAnActionEnabled = resourceClaimActions.Any(x => x.Enabled);
            if (!atleastAnActionEnabled)
            {
                context.AddFailure(propertyName, FeatureConstants.ResourceClaimOneActionNotSet);
            }
            else
            {
                var duplicates = resourceClaimActions.GroupBy(x => x.Name)
                              .Where(g => g.Count() > 1)
                              .Select(y => y.Key)
                              .ToList();
                foreach(var duplicate in duplicates)
                {
                    context.AddFailure(propertyName, $"{duplicate} action is duplicated.");
                }
                foreach (var action in resourceClaimActions)
                {
                    if(!dbActions.Any(actionName => actionName != null &&
                        actionName.Equals(action.Name, StringComparison.InvariantCultureIgnoreCase)))
                    {
                        context.AddFailure(propertyName, $"{action.Name} is not a valid action.");
                    }
                }
            }
        }
        else
        {
            context.AddFailure(propertyName, $"Actions can not be empty.");
        }
    }
}
