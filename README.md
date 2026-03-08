This is a versatile proxy server application developed in C# that supports various proxy protocols including HTTP, HTTPS (via tunneling), and SOCKS5. It offers advanced features such as reverse proxy capabilities with load balancing, content caching, and a robust filtering system based on domains, URLs, and MIME types. Designed for flexibility and control, it can be configured via command-line arguments and managed with simple runtime controls.

## Features

*   **Multi-protocol Support:** Handles HTTP, HTTPS (tunneling), and SOCKS5 proxy requests.
*   **Reverse Proxy & Load Balancing:** Can operate as a reverse proxy, distributing traffic among multiple backend servers.
*   **Content Caching:** Improves performance by caching frequently accessed content.
*   **Advanced Filtering:**
    *   **Domain Filtering:** Block or allow traffic based on specified domain names.
    *   **URL Filtering:** Control access to specific URLs.
    *   **MIME Type Filtering:** Restrict content delivery based on MIME types.
*   **Runtime Management:** Interactive console controls for restarting the proxy and clearing logs.
*   **Configurable:** All major features are configurable via command-line arguments.

## Getting Started

### Prerequisites

*   .NET SDK (version compatible with C# project)

### Building

To build the project, navigate to the project's root directory in your terminal and run:

```bash
dotnet build
```

### Running

After building, you can run the proxy server from the output directory (e.g., `bin/Debug/netX.Y/`) or directly using `dotnet run` from the project root. Below are examples of how to run the proxy with various configurations.

## Usage

The proxy server is configured using command-line arguments. Here's a list of available arguments:

| Argument           | Description                                                                 | Default Value | Example                                     |
| :----------------- | :-------------------------------------------------------------------------- | :------------ | :------------------------------------------ |
| `--port <number>`  | Specifies the port on which the proxy server will listen.                   | `8888`        | `--port 8080`                               |
| `--use-cache`      | Enables content caching.                                                    | `true`        | `--use-cache`                               |
| `--no-cache`       | Disables content caching.                                                   | `false`       | `--no-cache`                                |
| `--reverse`        | Activates reverse proxy mode.                                               | `false`       | `--reverse`                                 |
| `--backends <urls>`| Comma-separated list of backend server URLs for reverse proxy mode.         | `null`        | `--backends http://b1.com,http://b2.com`    |
| `--block-domains <domains>`| Comma-separated list of domains to block.                                   | `null`        | `--block-domains example.com,badsite.org`   |
| `--block-urls <urls>`| Comma-separated list of URLs to block.                                      | `null`        | `--block-urls /ads,/trackers`               |
| `--block-mimes <mimes>`| Comma-separated list of MIME types to block.                                | `null`        | `--block-mimes image/jpeg,video/mp4`        |
| `--no-filter`      | Disables all filtering.                                                     | `false`       | `--no-filter`                               |

### Examples

*   **Start a basic proxy on port 8080 with caching enabled:**
    ```bash
    dotnet run --project YourProjectName --port 8080 --use-cache
    ```

*   **Start a reverse proxy with two backends and no caching:**
    ```bash
    dotnet run --project YourProjectName --reverse --backends http://localhost:3001,http://localhost:3002 --no-cache
    ```

*   **Start a proxy blocking specific domains and MIME types:**
    ```bash
    dotnet run --project YourProjectName --block-domains facebook.com,twitter.com --block-mimes application/pdf
    ```

### Runtime Controls

While the proxy server is running, you can interact with it using keyboard shortcuts:

*   `Ctrl+R`: Restarts the proxy server, applying any changes made to its configuration (if supported by the underlying implementation, though typically this means re-initializing the proxy with its current arguments).
*   `Ctrl+L`: Clears the console logs.
*   `Ctrl+C`: Gracefully shuts down the proxy server and exits the application.

## Filtering

The proxy server includes a flexible filtering pipeline that can be configured to block unwanted content or access.

### Domain Filtering

Blocks requests to specified domain names. This is useful for preventing access to known malicious or undesirable websites.

### URL Filtering

Blocks requests to specific URL paths or patterns. This can be used to block specific resources like ad scripts or tracking pixels.

### MIME Type Filtering

Blocks content based on its MIME type. For example, you can prevent the download of certain file types like executables or large media files.

## Reverse Proxy / Load Balancing

When enabled with the `--reverse` flag and a list of `--backends`, the proxy acts as a reverse proxy. It distributes incoming requests among the specified backend servers, providing basic load balancing capabilities and enhancing the availability and scalability of your services.

## Caching

The `--use-cache` option enables an internal caching mechanism that stores responses from upstream servers. Subsequent requests for the same content can be served directly from the cache, significantly reducing latency and bandwidth usage.
