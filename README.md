# Watcher for syncing to Nextcloud

Nextcloud complicated client sync has never worked for me, so this program does a simple sync where the Nextcloud client could not.

## Getting Started

This .NET Core program will does a simple one-way sync to a remote Nextcloud / WebDAV server.

It will ***not*** sync deletes to the remote server (e.g. this will never send a DELETE). This is intentional as syncing deletes is dangerous and should not be done. Clients can always do a full sync later on, but syncing unintentional deletes is not recoverable. 

### Prerequisites

What things you need to install the software and how to install them

* .NET Core 3.1 for your platform


### Installing

* Git clone the repository.

* Copy `watcher.json` to `~/`

* Copy `watcher.db` to `~/`

* In `~/watcher.json` 

  * Change the `"Auth"` to your Nextcloud authentication key, e.g. the Nextcloud app password, `n31z7-myz1h-9xann-1a2Qz-93amk`, for user `foo` would be `"Auth": "foo:n31z7-myz1h-9xann-1a2Qz-93amk"`

  * Set `"Host": "https://cloud.userdomain.com/"` to your own Nextcloud domain.

  * This program assumes that you have a LAN endpoint for your Nextcloud to upload large (>100Mb) files, you can set this as `"HostLAN": "http://192.168.0.2:9999/"`. If you don't want this LAN setup, you can just set `"HostLAN"` to be the same as `"Host"`

* In `appsettings.json`

  * Change `Data Source=/Users/lam/watcher.db` to `Data Source=/<path_to_your_home_directory>/watcher.db`
 
  * Change `"WatcherConfFilePath": "/Users/lam/watcher.json"` to `"WatcherConfFilePath": "/<path_to_your_home_directory>/watcher.json"`

  * Change `"path": "/Users/lam/watcher_log.txt"` to `"path": "/<path_to_your_home_directory>/watcher_log.txt"`

  * Change `"WatchPath": "/Users/lam"` to be the local path that you want to watch.

  * Change  `"RemoteRootFolder": "lam/laptop"` be the remote path that you want to upload to.

  * Note that all local paths should be full paths, *not* relative paths.

* Optional

  * In `watcher.json`, `"RejectFilterPatterns"` contains regex patterns to ignore files. The default patterns are for Mac OS. You can add custom ignore patterns here.
  
  * In `appsettings.json`, `"RemapRemotePatterns"` contains mappings to change the default remote path for a given local path. An example is provided `appsettings.json`

Inside the repository root:

```
dotnet run --project watcher
```

and you can monitor the progress by looking at the `~/watcher_log.txt` log files. The log files are automatically rotated once they reach 10Mb. A maximum of 5 total log files are kept.

## Notes

* This has been tested against Nextcloud server 17, other WebDAV server may work, but will probably need code changes.

## Authors

* Kiet Lam - kietdlam@gmail.com

