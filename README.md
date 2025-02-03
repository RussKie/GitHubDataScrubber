# GitHub Data Scrubber

## Overview

GitHub Data Scrubber is a CLI tool designed to extract and scrub GitHub issues and discussions from a JSON file. The tool processes the data to remove sensitive information, clean up the content, and output the results in a CSV format. This tool is particularly useful for analyzing GitHub data while ensuring privacy and data cleanliness.

## Features

- Extracts issues and comments from a GitHub data JSON file.
- Scrubs content to remove sensitive information and unwanted data.
- Outputs the cleaned data in a CSV format.
- Supports specifying input and output paths via command-line arguments.

## Prerequisites

- .NET 9.0 SDK or later.
- The `ghdump` tool to generate the input JSON file (see [ghdump](https://github.com/davidfowl/feedbackflow)).

## Building the Tool

1. Clone the repository:

    ```
    git clone <repository-url>
    cd <repository-directory>
    ```

2. Restore the dependencies and build the project:

    ```
    dotnet restore
    dotnet build
    ```

## Running the Tool

To run the tool, use the following command:

```
dotnet run -- -i <input-file> [-o <output-directory>]
```

### Command-Line Arguments

- `-i, --input` (required): Path to the GitHub data JSON file.
- `-o, --output` (optional): Directory where the results will be written. Defaults to the current working directory.

### Example

```
dotnet run -- -i github_data.json -o output_directory
```

This command will process the `github_data.json` file, scrub the content, and save the results in the `output_directory`.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
