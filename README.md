# Inedo Git Extensions

[![Build status](https://buildmaster.inedo.com/api/ci-badges/image?API_Key=badges&$ApplicationId=5)](https://buildmaster.inedo.com/api/ci-badges/link?API_Key=badges&$ApplicationId=5)

This repository has code for four Git extensions that enable BuildMaster and/or Otter to interact with Git repositories:

## Git Extension

 - Cloning/export Git repositories
 - Tagging source in Git repositories
 - Git-based rafts (Otter only)

Each extension in this repository also contains the above operations. The following extensions add functionality specific to the hosting provider:

## GitHub Extension
 
 - Issue synchronization (BuildMaster only)
 - Creating and uploading assets to GitHub releases
 - Setting CI status
 - Creating milestones
 - Creating issues & adding comments
 - Closing issues
 
Refer to the [GitHub BuildMaster documentation](https://docs.inedo.com/docs/buildmaster/integrations/github) for more information.

## GitLab Extension

 - Issue synchronization (BuildMaster only)
 - Creating releases in GitLab
 - Creating milestones
 - Creating issues & notes
 - Closing issues
 
Refer to the [GitLab BuildMaster documentation](https://docs.inedo.com/docs/buildmaster/integrations/gitlab for more information.
 
## Azure DevOps Extension

 - Issue (work item) synchronization (BuildMaster only)
 - Creating work items
 - Download/import build artifacts
 - Queue builds
 
Refer to the [Azure DevOps BuildMaster documentation](https://docs.inedo.com/docs/buildmaster/integrations/azure-devops) for more information.

## Documentation

Documentation for specific operations is available within BuildMaster or Otter once the desired extension is installed. Browse the Documentation page under the User Icon for more information.

## Installation Instructions

To install this extension, visit the Extensions page within the applicable Inedo software.

For manual installation, visit the GitHub releases section of this repository to download the desired version and follow the [extension build and deployment](https://inedo.com/support/documentation/various/inedo-sdk/creating#building-deploying) documentation on the Inedo website.

## Release Notes

Visit the GitHub issues page of this repository for release notes.

## Contributing

We are happy to consider contributions in many forms (bug reports, feature requests, pull requests, etc.). For more information, visit the [Contributing](https://inedo.com/open/contributing) section on the Inedo website.
