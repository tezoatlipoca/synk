# synk

## An anonymous key oriented, self-hosted data synchronization dropbox. 

An ASP.NET single binary microwebservice that can store and retreive data using unique keys.
Intended as a simple, self-hosted way to 
* provide _data synchronization_ services between nodes of an application that know a shared key
* provide anonymous, arbitrary data dropbox facilities using a secret key as part of the URI

What information you PUT or GET is up to you; so long as you use the correct key, any one 
or thing can access the shared data. The key might be data decryption keys, or GUIDs, 
or whatever makes sense to you.

`synk` does not know or care what data you store, whether you encrypt it or not, or how 
you prevent data trampling - is entirely up to you. 

Two endpoints:

## PUT `/blob/{key}`
and the body of your request will be saved in a file at `$synkstore/<SHA256 hash of key>`

## GET `/blob/{key}` 
and the body of your response will be the contents of the file at `$synkstore/<SHA256 has of key>`

`HTTP 204` is returned if `{key}` is invalid or there is no data stored for `{key}`.
`HTTP 413` or `Payload Too Large` is returned if you try and `PUT` to a `{key}` but its size exceeds the configured storage space. 

- `{key}` has to be URL encoded
- URLunencoded, `{key}` can be at most 512 bytes long - but this is just long enough to be an RSA key.
- No restriction on WHAT you use for `{key}` - it could be a GUID, RSA or ECDSA key.
- `$synkstore` is configurable as is how _large_ it can be.
- If the size of the `$synkstore` is larger than `maxsynkstoresize` on startup you get a warning but everything still works; you may not be able to upload anything else.
- Setting `maxsynkstoresize` to `0` and restarting, effectively sets your `synk` node to READ ONLY. 

## Usage
```
Usage: ./synk(.exe) -- [options]
Options:
--port=PORT			Port to listen on. Default is 5000
--bind=IP			  IP address to bind to. Default is *
--hostname=URL	URL to use in links. Default is http://localhost
--runlevel=LEVEL			Log level. Default is Information
--sitecss=URL		URL to the site stylesheet. Default is null (UNTESTED)
--sitepng=URL		URL to the site favicon.ico. Default is null (UNTESTED)
--synkstore=PATH		  Path to the blob store. Default is <working directory>/.synkstore)
--maxsynkstoresize=BYTES  Maximum size of the synkstore in bytes. Default is 10MB.
--siteinfo=<about YOU>  Is displayed on the /About page - so people can reach you. 
```

## Data Privacy
`synk` does not know, or care about what data you store - whether its encrypted, ascii or binary; it just writes the bytes you give it to disk.
As such, the blobs in the `synkstore` are _technically_ visible to the server/admin. 
A list of all "valid" keys and associated blobs is maintained in the `synkstore`, but there is no way to obtain the keys without access to 
that folder. Having said that, if people use very short keys (e.g. `abc123`) it is possible, in theory, for someone to bruteforce guess
one and see whatever is stored there, but if keys longer than the recommended 16 bytes are used, this becomes unlikely. 

Ultimately however, if you know the a valid key, you can retreive the data stored against it. 

There is no way (currently) to delete a key and its associate blob file - but yo can overwrite it with garbage. 

## Synkstore Blob Consideration
The default `maxsynkstoresize` is `10485760` bytes or 10MB. On startup `synk` does some housecleaning and reindexes all the blobs and keys and gets rid of orphans (not that there should be); it also calculates the _size_ of all blobs. If the byte size of all existing blobs is greater than `maxsynkstoresize`, you get a warning, but 
the application continues; you simply won't be allowed to add anything else. The side effect of this is to make your instance effectively read only. 
Key data blob files cannot be deleted (yet), but they can be overwritten by _smaller_ data blobs. 

## Future Work
- DELete keys (and their blobs)
- better IP logging
- IP whitelisting
- provide additional restrictions/gatekeeping - right now anyone can store anything.
- further obsfucate (hash?) the data written to disk so the server/admin cannot see the content.
- HARD_MODE - where not even the key->filenames are known to the server/admin.
- add data retention limits; prune older keys etc.; allow to set an expiry on a key when "touched". 

## Why are you doing this? 
"You're effectively writing your own cloud storage, you're insane" - I know, fun, right?
I was writing a terminal program and wanted a way to synchronize configuration parameters between the 
same program across multiple computers. There are TONS of utilities and services that do this
of course, but unlike client-server, p2p like Portal (https://github.com/SpatiumPortae/portal)
or existing cloud synchronization platforms like Dropbox, I 
* didn't want to prompt for or store login credentials
* didn't want to generate magic/secrets every time
* wanted to use a self-hosted solution
* not have to rely on a "synchronization" process.
.. and so, voila.

The idea is that I can configure my `synk` instance and a shared `key` on all of my computers
and when needed simply get/put data on demand. I can now add rudimentary synchronization metadata
to whatever I want to store.

## How is this not Redis/Valkey with an API up front? 
The blobs themselves aren't stored in memory; only a Dictionary of key+blob_file_hashs. Redis usually requires
2-3x the data storage in RAM; this way RAM is minimal, you're limited by nonvolatile storage space.
(at perhaps the sake of speed, there's no caching of blob data)
Each blob_file_hash and key is 64 bytes, so each stashed blob consumes 128 bytes, so 8.4M keys per 1GB of RAM
And one less dependency; Redis and Valkey are very good and tackle a whole lot of problems
that this is not intended to solve. 
