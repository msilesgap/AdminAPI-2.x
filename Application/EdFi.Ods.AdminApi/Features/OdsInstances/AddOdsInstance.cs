// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.

using AutoMapper;
using EdFi.Ods.AdminApi.Helpers;
using EdFi.Ods.AdminApi.Infrastructure;
using EdFi.Ods.AdminApi.Infrastructure.Database.Commands;
using EdFi.Ods.AdminApi.Infrastructure.Database.Queries;
using EdFi.Ods.AdminApi.Infrastructure.Helpers;
using FluentValidation;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace EdFi.Ods.AdminApi.Features.OdsInstances;

public class AddOdsInstance : IFeature
{
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        AdminApiEndpointBuilder
           .MapPost(endpoints, "/odsInstances", Handle)
           .WithDefaultDescription()
           .WithRouteOptions(b => b.WithResponseCode(201))
           .BuildForVersions(AdminApiVersions.V2);
    }

    public async Task<IResult> Handle(Validator validator, IAddOdsInstanceCommand addOdsInstanceCommand, IMapper mapper, AddOdsInstanceRequest request)
    {
        await validator.GuardAsync(request);
        var addedProfile = addOdsInstanceCommand.Execute(request);
        return Results.Created($"/odsInstances/{addedProfile.OdsInstanceId}", null);
    }

    [SwaggerSchema(Title = "AddOdsInstanceRequest")]
    public class AddOdsInstanceRequest : IAddOdsInstanceModel
    {
        [SwaggerSchema(Description = FeatureConstants.OdsInstanceName, Nullable = false)]
        public string? Name { get; set; }
        [SwaggerSchema(Description = FeatureConstants.OdsInstanceInstanceType, Nullable = false)]
        public string? InstanceType { get; set; }
        [SwaggerSchema(Description = FeatureConstants.OdsInstanceConnectionString, Nullable = false)]
        public string? ConnectionString { get; set; }

    }

    public class Validator : AbstractValidator<IAddOdsInstanceModel>
    {
        private readonly IGetOdsInstancesQuery _getOdsInstancesQuery;
        private readonly string _databaseEngine;
        public Validator(IGetOdsInstancesQuery getOdsInstancesQuery, IOptions<AppSettings> options)
        {
            _getOdsInstancesQuery = getOdsInstancesQuery;
            _databaseEngine = options.Value.DatabaseEngine ?? throw new NotFoundException<string>("AppSettings", "DatabaseEngine");

            RuleFor(m => m.Name)
                .NotEmpty()
                .Must(BeAUniqueName)
                .WithMessage(FeatureConstants.OdsInstanceAlreadyExistsMessage);

            RuleFor(m => m.InstanceType).NotEmpty();

            RuleFor(m => m.ConnectionString)
                .NotEmpty();

            RuleFor(m => m.ConnectionString)
                .Must(BeAValidConnectionString)
                .WithMessage(FeatureConstants.OdsInstanceConnectionStringInvalid)
                .When(m => !string.IsNullOrEmpty(m.ConnectionString));
        }

        private bool BeAUniqueName(string? name)
        {
            return _getOdsInstancesQuery.Execute().All(x => x.Name != name);
        }

        private bool BeAValidConnectionString(string? connectionString)
        {
            return ConnectionStringHelper.ValidateConnectionString(_databaseEngine, connectionString);
        }
    }
}
