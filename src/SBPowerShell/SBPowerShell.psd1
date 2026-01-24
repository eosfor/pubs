@{
    # Script module or binary module file associated with this manifest.
    RootModule = 'SBPowerShell.dll'

    # Version number of this module.
    ModuleVersion = '0.1.0'

    # ID used to uniquely identify this module.
    GUID = 'c6d9d8f5-6c32-4e3a-8d8d-1d2076df0c3e'

    # Author of this module
    Author = 'pubs'

    # Company or vendor of this module
    CompanyName = 'pubs'

    # A description of this module
    Description = 'PowerShell cmdlets for Azure Service Bus and Service Bus Emulator.'

    # Minimum version of the Windows PowerShell engine required by this module
    PowerShellVersion = '7.0'

    CompatiblePSEditions = @('Core')

    # Modules that must be imported into the global environment prior to importing this module
    RequiredModules = @()

    # Assemblies that must be loaded prior to importing this module
    RequiredAssemblies = @()

    # Functions to export from this module
    FunctionsToExport = @()

    # Cmdlets to export from this module
    CmdletsToExport = @('New-SBMessage', 'Send-SBMessage', 'Receive-SBMessage', 'Clear-SBQueue', 'Clear-SBSubscription')

    # Variables to export from this module
    VariablesToExport = @()

    # Aliases to export from this module
    AliasesToExport = @()

    PrivateData = @{
        PSData = @{
            Tags = @('Azure', 'ServiceBus', 'Messaging')
            ProjectUri = ''
            LicenseUri = ''
        }
    }
}
