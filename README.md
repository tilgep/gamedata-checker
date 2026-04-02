Checks [CS2Fixes](https://github.com/Source2ZE/CS2Fixes) main branch windows and linux signatures when a [CS2](https://store.steampowered.com/app/730/CounterStrike_2/) update happens.  
Sends info to a discord webhook.  
  
Code taken from [DepotDownloader](https://github.com/SteamRE/DepotDownloader) to download CS2 binaries.  

### Setup

- Download the latest [Release](https://github.com/tilgep/gamedata-checker/releases)
- Extract the contents somewhere
- Clone your desired CS2Fixes repo somewhere

- In the `Data/GamedataChecker.json` file set the following:
  - Webhook URL
  - GitHub Username
  - GitHub Password
  - Path to CS2Fixes git repo
- Run `GamedataChecker` exe or binary 
