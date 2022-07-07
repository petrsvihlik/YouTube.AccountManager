# YouTube Account Manager
A console utility allowing migration of data (playlists, liked videos, watching history) from one account to another. 


## Prerequisitess
- [.NET 6.0](https://dotnet.microsoft.com/en-us/download)
- YouTube Data API access

## Setup
- Clone the repo
- Go to the Google Cloud Console -> APIs & Services
- Create a new OAuth 2.0 Client IDs
- Download the JSON and save it to `YouTubeAccountMerger\YouTube.AccountMigrator` folder
- Manage user secrets and [set your account IDs](https://support.google.com/youtube/answer/3250431?hl=en)
```json
    { 
      "src_account_id": "<YOUR_SOURCE_ID>",
      "target_account_id": "<YOUR_TARGET_ID>"
    }
```
- Build the project and run

## Running the app
- Choose the mode
  - Preview - lists all the data that would be migrated
  - Migrate - performs the actual migration
- Log in with the source account
- Log in with the target account (required only for the actual migration)
- Choose the data you'd like to migrate
- Watch the output in the console
