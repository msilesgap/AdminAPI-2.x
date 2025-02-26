// SPDX-License-Identifier: Apache-2.0
// Licensed to the Ed-Fi Alliance under one or more agreements.
// The Ed-Fi Alliance licenses this file to you under the Apache License, Version 2.0.
// See the LICENSE and NOTICES files in the project root for more information.

using EdFi.Admin.DataAccess.Models;
using EdFi.Ods.AdminApi.Infrastructure;
using EdFi.Ods.AdminApi.Infrastructure.Database.Commands;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EdFi.Ods.AdminApi.DBTests.Database.CommandTests;

[TestFixture]
public class AddApplicationCommandTests : PlatformUsersContextTestBase
{
    [Test]
    public void ShouldFailForInvalidVendor()
    {
        var vendor = new Vendor
        {
            VendorNamespacePrefixes = new List<VendorNamespacePrefix> { new VendorNamespacePrefix { NamespacePrefix = "http://tests.com" } },
            VendorName = "Integration Tests"
        };

        Save(vendor);

        Transaction(usersContext =>
        {
            var command = new AddApplicationCommand(usersContext);
            var newApplication = new TestApplication
            {
                ApplicationName = "Production-Test Application",
                ClaimSetName = "FakeClaimSet",
                ProfileIds = null,
                VendorId = 0
            };

            Assert.Throws<InvalidOperationException>(() => command.Execute(newApplication));
        });
    }

    [Test]
    public void ProfileShouldBeOptional()
    {
        var vendor = new Vendor
        {
            VendorNamespacePrefixes = new List<VendorNamespacePrefix> { new VendorNamespacePrefix { NamespacePrefix = "http://tests.com" } },
            VendorName = "Integration Tests"
        };

        var odsInstance = new OdsInstance
        {
            Name = "Test Instance",
            InstanceType = "Ods",
            ConnectionString = "Data Source=(local);Initial Catalog=EdFi_Ods;Integrated Security=True;Encrypt=False"
        };

        Save(vendor, odsInstance);

        AddApplicationResult result = null;

        Transaction(usersContext =>
        {
            var command = new AddApplicationCommand(usersContext);
            var newApplication = new TestApplication
            {
                ApplicationName = "Test Application",
                ClaimSetName = "FakeClaimSet",
                ProfileIds = null,
                VendorId = vendor.VendorId,
                EducationOrganizationIds = new List<int> { 12345, 67890 },
                OdsInstanceIds = new List<int> { odsInstance.OdsInstanceId }, 
            };

            result = command.Execute(newApplication);
        });

        Transaction(usersContext =>
        {
            var persistedApplication = usersContext.Applications
            .Include(a => a.ApplicationEducationOrganizations)
            .Include(a => a.Vendor)
            .Include(a => a.ApiClients)
            .FirstOrDefault(a => a.ApplicationId == result.ApplicationId);

            persistedApplication.ClaimSetName.ShouldBe("FakeClaimSet");
            persistedApplication.Profiles.Count.ShouldBe(0);
            persistedApplication.ApplicationEducationOrganizations.Count.ShouldBe(2);
            persistedApplication.ApplicationEducationOrganizations.All(o => o.EducationOrganizationId == 12345 || o.EducationOrganizationId == 67890).ShouldBeTrue();

            persistedApplication.Vendor.VendorId.ShouldBeGreaterThan(0);
            persistedApplication.Vendor.VendorId.ShouldBe(vendor.VendorId);

            persistedApplication.ApiClients.Count.ShouldBe(1);
            var apiClient = persistedApplication.ApiClients.FirstOrDefault();
            apiClient.Name.ShouldBe("Test Application");
            apiClient.ApplicationEducationOrganizations.All(o => o.EducationOrganizationId == 12345 || o.EducationOrganizationId == 67890).ShouldBeTrue();
            apiClient.Key.ShouldBe(result.Key);
            apiClient.Secret.ShouldBe(result.Secret);
        });
    }

    [Test]
    public void ShouldExecute()
    {
        const string odsInstanceName = "Test Instance";
        var vendor = new Vendor
        {
            VendorNamespacePrefixes = new List<VendorNamespacePrefix> { new VendorNamespacePrefix { NamespacePrefix = "http://tests.com" } },
            VendorName = "Integration Tests"
        };

        var profile = new Profile
        {
            ProfileName = "Test Profile"
        };

        var odsInstance = new OdsInstance
        {
            Name = odsInstanceName,
            InstanceType = "Ods",
            ConnectionString = "Data Source=(local);Initial Catalog=EdFi_Ods;Integrated Security=True;Encrypt=False"
        };

        Save(vendor, profile, odsInstance);

        AddApplicationResult result = null;
        Transaction(usersContext =>
        {
            var command = new AddApplicationCommand(usersContext);
            var newApplication = new TestApplication
            {
                ApplicationName = "Test Application",
                ClaimSetName = "FakeClaimSet",
                ProfileIds = new List<int>() { profile.ProfileId },
                VendorId = vendor.VendorId,
                EducationOrganizationIds = new List<int> { 12345, 67890 },
                OdsInstanceIds = new List<int> { odsInstance.OdsInstanceId },
            };

            result = command.Execute(newApplication);
        });

        Transaction(usersContext =>
        {
            var persistedApplication = usersContext.Applications
            .Include(a => a.ApplicationEducationOrganizations)
            .Include(a => a.Vendor)
            .Include(a => a.Profiles)
            .Include(a => a.ApiClients).Single(a => a.ApplicationId == result.ApplicationId);
            persistedApplication.ClaimSetName.ShouldBe("FakeClaimSet");
            persistedApplication.Profiles.Count.ShouldBe(1);
            persistedApplication.Profiles.First().ProfileName.ShouldBe("Test Profile");
            persistedApplication.ApplicationEducationOrganizations.Count.ShouldBe(2);
            persistedApplication.ApplicationEducationOrganizations.All(o => o.EducationOrganizationId == 12345 || o.EducationOrganizationId == 67890).ShouldBeTrue();

            persistedApplication.Vendor.VendorId.ShouldBeGreaterThan(0);
            persistedApplication.Vendor.VendorId.ShouldBe(vendor.VendorId);

            persistedApplication.ApiClients.Count.ShouldBe(1);
            var apiClient = persistedApplication.ApiClients.First();
            apiClient.Name.ShouldBe("Test Application");
            apiClient.ApplicationEducationOrganizations.All(o => o.EducationOrganizationId == 12345 || o.EducationOrganizationId == 67890).ShouldBeTrue();
            apiClient.Key.ShouldBe(result.Key);
            apiClient.Secret.ShouldBe(result.Secret);

            var persistedApiOdsInstances = usersContext.ApiClientOdsInstances.Where(a => a.ApiClient.ApiClientId == apiClient.ApiClientId).ToList();

            persistedApiOdsInstances.ShouldNotBeNull();
            persistedApiOdsInstances.First().ApiClient.ApiClientId.ShouldBe(apiClient.ApiClientId);
        });

        Transaction(usersContext =>
        {
            var persistedApplication = usersContext.Applications
            .Include(p => p.ApiClients)
            .Single(a => a.ApplicationId == result.ApplicationId);
            var apiClient = persistedApplication.ApiClients.First();
            var apiClientOdsInstance = usersContext.ApiClientOdsInstances
            .Include(ac => ac.OdsInstance)
            .Include(ac => ac.ApiClient)
            .FirstOrDefault(o => o.OdsInstance.OdsInstanceId == odsInstance.OdsInstanceId && o.ApiClient.ApiClientId == apiClient.ApiClientId);
            apiClientOdsInstance.ApiClient.ApiClientId.ShouldBe(apiClient.ApiClientId);
            apiClientOdsInstance.OdsInstance.OdsInstanceId.ShouldBe(odsInstance.OdsInstanceId);
        });
        
    }

    private class TestApplication : IAddApplicationModel
    {
        public string ApplicationName { get; set; }
        public int VendorId { get; set; }
        public string ClaimSetName { get; set; }
        public IEnumerable<int> ProfileIds { get; set; }
        public IEnumerable<int> EducationOrganizationIds { get; set; }
        public IEnumerable<int> OdsInstanceIds { get; set; }
    }
}
