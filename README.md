# synk

## An anonymous key oriented, self-hosted data synchronization dropbox. 

An ASP.NET single binary microwebservice that can store and retreive data using a unique key.
Intended as a simple, self-hosted way to provide built in _data synchronization_ services 
between instances of your program when direct synchronous communication isn't possible. 
What information you PUT or GET is up to you; so long as you use the correct key, any one 
or thing can access the shared data. The key might be data decryption keys, or GUIDs, 
or whatever makes sense to your application.

`synk` only provides a file based asynchronous data dropbox; stored data is written
as-is to the backing store; if/how you encrypt it, how you prevent data trampling
is entirely up to you. 

Two endpoints:

## PUT `/blob/{key}`
and the body of your request will be saved in a file at `$synkstore/<SHA256 hash of key>`

## GET `/blob/{key}` 
and the body of your response will be the contents of the file at `$synkstore/<SHA256 has of key>`

`HTTP 204` is returned if `{key}` is invalid or there is no data stored for `{key}`.

- `{key}` has to be URL encoded
- URLunencoded, `{key}` can be at most 512 bytes long - but this is just long enough to be an RSA key.
- No restriction on WHAT you use for `{key}` - it could be a GUID
- `$synkstore` is configurable (but no size restrictions are enforced - yet)

## Usage
```
Usage: ./synk(.exe) -- [options]
Options:
--port=PORT			Port to listen on. Default is 5000
--bind=IP			  IP address to bind to. Default is *
--hostname=URL	URL to use in links. Default is http://localhost
--runlevel=LEVEL			Log level. Default is Information
--sitecss=URL		URL to the site stylesheet. Default is null
--sitepng=URL		URL to the site favicon.ico. Default is null
--synkstore=PATH		  Path to the blob store. Default is <working directory>/.synkstore)
```

## Future Work
- better IP logging
- `$synkstore` folder size restriction
- IP whitelisting
- provide additional restrictions/gatekeeping - right now anyone can store anything.
