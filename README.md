# MetaProject is no longer needed

This repository tied the OpenMacroBoard repositories together with submodules and held the build
script. All of that is now in the [StreamDeckSharp repository](https://github.com/OpenMacroBoard/StreamDeckSharp).

| What | New place |
| --- | --- |
| SDK and the other libraries | [`src`](https://github.com/OpenMacroBoard/StreamDeckSharp/tree/main/src) |
| Examples | [`examples`](https://github.com/OpenMacroBoard/StreamDeckSharp/tree/main/examples) |
| Build (replaces the Nuke build script) | [`Dockerfile`](https://github.com/OpenMacroBoard/StreamDeckSharp/blob/main/Dockerfile) |
| Solution with all projects | [`StreamDeckSharp.sln`](https://github.com/OpenMacroBoard/StreamDeckSharp/blob/main/StreamDeckSharp.sln) |

## Why

The project was spread over several repositories: the SDK, the Stream Deck provider, the examples
and a meta project that tied them together with submodules. A change that touched more than one of
them needed several commits, pull requests and releases that had to match. Now everything is
developed, tested and released in one place.

## What happens to this repository

- It is no longer updated and will be archived (read-only).
- The history was taken along: every commit of this repository is also part of the new one.
- The code here stays as it was at commit `fad34ab`, tagged `pre-ai/metaproject` in the new repository.
- Please open issues and pull requests in the [new repository](https://github.com/OpenMacroBoard/StreamDeckSharp/issues).

This repository never published a package, so nothing changes for projects that use the libraries.

This note was written with the help of AI; the code in this repository was not. See the
[AI disclosure](https://github.com/OpenMacroBoard/StreamDeckSharp/blob/main/AI-DISCLOSURE.md) of the project.
