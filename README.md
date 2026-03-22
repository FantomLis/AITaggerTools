# AI Tagger Tools
> [!WARNING]
> This project is not perfect, please be aware of that. This project is fully written by junior-developer with no experience.
> Maybe sometimes it will crash with weird reason or not properly work with .xmp files.
> Maybe some decision in this project is not good. </br>
> You can always open issue with your problem and we try to help you.

Small CLI-tool, that uploads your files to AI model endpoint, fetches tags and apply it to tags in .xmp file. </br>
You can download AITaggerCLI from releases or build it yourself.
## Usage
```bash
Description:
  CLI-tool for AI tags applying.
  Original purpose of that app is to allow custom AI models to be used for smart search in Immich.
  Requires AITaggerAPI REST API endpoint to send your images/videos.
  When using folder, tool will scan all folders inside and scan every file.

Usage:
  AITaggerCLI [options]

Options:
  -i, --input <input> (REQUIRED)                     Input file (should be video or image) or folder. Multiple inputs allowed.
  -e, --endpoint <endpoint>                          REST API endpoint, that supports POST /desc/upload for image uploading, GET /info for endpoint info and GET /desc/fetch for fetching image tags.
  -o, --output <output>                              Target file for .xmp files. Will be ignored when multiple inputs or directory as input is used.
  -b, --backup <backup>                              Move original file to other location with old_[DATE] prefix.
  -q, -quick, --quick-apply                          Checks if any tag in .xmp file has endpoint id and skips file if so.
  -u, -ui, --webui                                   Starts WebUI instead of CLI.
  -c, --clear <clear>                                Removes all tags with this endpoint id.
  -iee, --ignore-endpoint-extensions                 Ignores file extension from endpoint and forces to send all files. Be aware that this will not process your file if it isn't supported.
  -lf, --limit, --limit-filecount <limit-filecount>  Limits how many files will be uploaded to endpoint before requesting result. [default: -1]
  -?, -h, --help                                     Show help and usage information
  --version                                          Show version information
```

## Examples
#### Process single file
`AITaggerCLI.exe -i ./image.png -e http:///localhost:8000`
#### Process multiple files
`AITaggerCLI.exe -i ./image.png -i ./image2.jpg -e http:///localhost:8000`
#### Process all files in folder
`AITaggerCLI.exe -i ./folder_with_images -e http:///localhost:8000`
#### Disable quick tag applying
`AITaggerCLI.exe -q false -i ./folder_with_images -e http:///localhost:8000`
#### Clean all tags from files
`AITaggerCLI.exe -c ExampleEndpointId -i ./folder_with_images -e http:///localhost:8000`
#### Limit file upload count
`AITaggerCLI.exe -lf 10 -i ./image.png -i ./image2.jpg -e http:///localhost:8000`

## How to create endpoint?
AITaggerCLI uses AITagger REST API for uploading and fetching tags for files. To properly implement this API, you should create 3 endpoints: 
- /desc/upload: Receive single file in a form, responses with new server filename
- /desc/fetch: Receive multiple server filenames in JSON string array, responses with MultiFileResponse structure
- /info: Responses with EndpointInfo structure
### EndpointInfo
JSON-object
```json
{"endpointId":"some-endpoint-id","supportedFiletypes":["png", "some-other-extension-without-dot"]}
```
### MultiFileResponse
JSON-object, every tag should be separated by ` ,`
```json
{"endpointId":"some-endpoint-id","files":[{"filename":"filename.extension","tagsInfo":"Tag, Tag2, Tag3","error":null},{"filename":"bad.file","tagsInfo":null,"error":"Error message"}]}
```
> [!NOTE]
> AITaggerAPI-Example is example project, that fully implements this API and also have method for splitting video and animated images to frames.

## Why? 
Immich doesn't support custom model, but supports .xmp sidecar files and finding by tags. So I just made small layer, that can take any photo or video and add AI tags to it.
