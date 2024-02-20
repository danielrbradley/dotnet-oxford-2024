using Pulumi;
using Pulumi.AzureNative.Resources;
using Pulumi.AzureNative.Storage;
using Pulumi.AzureNative.Storage.Inputs;
using System.Collections.Generic;

return await Pulumi.Deployment.RunAsync(() =>
{
    // Create an Azure Resource Group
    var resourceGroup = new ResourceGroup("resourceGroup");

    var registry = new Pulumi.AzureNative.ContainerRegistry.Registry("registry", new Pulumi.AzureNative.ContainerRegistry.RegistryArgs
    {
        ResourceGroupName = resourceGroup.Name,
        Sku = new Pulumi.AzureNative.ContainerRegistry.Inputs.SkuArgs
        {
            Name = Pulumi.AzureNative.ContainerRegistry.SkuName.Basic,
        },
        AdminUserEnabled = true,
    });

    var credentials = Pulumi.AzureNative.ContainerRegistry.ListRegistryCredentials.Invoke(new Pulumi.AzureNative.ContainerRegistry.ListRegistryCredentialsInvokeArgs
    {
        RegistryName = registry.Name,
        ResourceGroupName = resourceGroup.Name,
    });

    var username = credentials.Apply(c => c.Username);
    var password = credentials.Apply(c => c.Passwords[0].Value);

    var image = new Pulumi.Docker.Image("image", new Pulumi.Docker.ImageArgs
    {
        ImageName = Pulumi.Output.Format($"{registry.LoginServer}/hello-world"),
        Build = new Pulumi.Docker.Inputs.DockerBuildArgs
        {
            Context = "../albumapi",
            Platform = "linux/amd64",
        },
        Registry = new Pulumi.Docker.Inputs.RegistryArgs
        {
            Server = registry.LoginServer,
            Username = username,
            Password = password,
        },
    });

    var environment = new Pulumi.AzureNative.App.ManagedEnvironment("environment", new Pulumi.AzureNative.App.ManagedEnvironmentArgs
    {
        ResourceGroupName = resourceGroup.Name,
    });

    var containerApp = new Pulumi.AzureNative.App.ContainerApp("app", new Pulumi.AzureNative.App.ContainerAppArgs
    {
        ResourceGroupName = resourceGroup.Name,
        ManagedEnvironmentId = environment.Id,
        Template = new Pulumi.AzureNative.App.Inputs.TemplateArgs
        {
            Containers = new[]
            {
                new Pulumi.AzureNative.App.Inputs.ContainerArgs
                {
                    Name = "albumapi",
                    Image = image.ImageName,
                }
            },
        },
        Configuration = new Pulumi.AzureNative.App.Inputs.ConfigurationArgs
        {
            Ingress = new Pulumi.AzureNative.App.Inputs.IngressArgs
            {
                External = true,
                TargetPort = 8080,
            },
            Registries = new Pulumi.AzureNative.App.Inputs.RegistryCredentialsArgs
            {
                Server = registry.LoginServer,
                Username = username,
                PasswordSecretRef = "password",
            },
            Secrets = new[]
            {
                new Pulumi.AzureNative.App.Inputs.SecretArgs
                {
                    Name = "password",
                    Value = password,
                },
            }
        },
    });

    // Export the primary key of the Storage Account
    return new Dictionary<string, object?>
    {
        ["resourceGroupName"] = resourceGroup.Name
    };
});