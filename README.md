Some small app, that adds tags from AI model. 
> [!WARNING]
> This app is in development, except bugs. (also this is very poorly built app, I still working on it)
## How to use: 
1. Download app
2. Run (or create) API-backend for your model (You can use ExampleAPI as a base), that
    - Have POST /desc route
    - Body is AI image/video tags, thats separated by ", "
    - Endpoint-ID header is ID for endpoint
4. Run AITaggerCLI with -i (input filepath) and -e (endpoint) args
Program will create .xmp file with AI tags from API with its ID.

Why? - Immich doesn't support custom model, but supports .xmp sidecar files and finding by tags. So I just made small layer, that can take any photo or video and add AI smart search tags. 
