import { createClient } from "@hey-api/openapi-ts";

// Start notifying user we are generating the TypeScript client
console.log("Generating OpenAPI client...");

const args = process.argv.slice(2);
const swaggerUrl = args[0];
const outputPath = args[1];
const laneIndex = args.indexOf("--lane");
const lane = laneIndex !== -1 && args[laneIndex + 1] ? args[laneIndex + 1] : "v17";

if (lane !== "v17" && lane !== "v18") {
  console.error(`ERROR: Unsupported client lane: ${lane}`);
  process.exit(1);
}

// Find --includeTags and --excludeTags in the arguments
const includeIndex = args.indexOf("--includeTags");
const excludeIndex = args.indexOf("--excludeTags");

const includeTags =
  includeIndex !== -1 && args[includeIndex + 1]
    ? args[includeIndex + 1].split(",")
    : undefined;
const excludeTags =
  excludeIndex !== -1 && args[excludeIndex + 1]
    ? args[excludeIndex + 1].split(",")
    : undefined;

if (swaggerUrl === undefined || outputPath === undefined) {
  console.error(`ERROR: Missing URL to OpenAPI spec or output path`);
  console.error(
    `Please provide the URL and output path as the first two arguments.`
  );
  console.error(
    `Example: node generate-openapi.js https://.../swagger.json ./src/api`
  );
  process.exit(1);
}

// Start checking to see if we can connect to the OpenAPI spec
console.log("Ensure your Umbraco instance is running");
console.log(`Fetching OpenAPI definition from ${swaggerUrl}`);

fetch(swaggerUrl)
  .then(async (response) => {
    if (!response.ok) {
      console.error(
        `ERROR: OpenAPI spec returned with a non OK (200) response: ${response.status} ${response.statusText}`
      );
      console.error(
        `The URL to your Umbraco instance may be wrong or the instance is not running`
      );
      console.error(
        `Please verify or change the URL in the package.json for the script generate-openapi`
      );
      console.error(
        `Or review back office logs. Swagger cannot generate a schema with route conflicts or duplicate API attributes. ` +
          "The Management API security filter already documents 401 and 403; " +
          "do not add those ProducesResponseType attributes to endpoints, because duplicate response keys make schema generation fail."
      );
      process.exit(1);
    }

    console.log(`OpenAPI spec fetched successfully`);
    console.log(
      `Calling hey-api to generate TypeScript client`
    );

    const config = {
      logs: {
        level: "debug",
      },
      input: {
        path: swaggerUrl,
      },
      output: {
        indexFile: false,
        path: outputPath,
      },
      plugins: [
        '@hey-api/client-fetch',
        {
          name: '@hey-api/sdk',
          operations: {
            strategy: 'byTags',
            containerName: '{{name}}Service',
          },
        },
        {
          name: '@hey-api/typescript',
          enums: 'typescript',
        },
      ],
    };

    if (includeTags || excludeTags) {
      config.input.filters = {
        tags: {
          ...(includeTags && { include: includeTags }),
          ...(excludeTags && { exclude: excludeTags }),
        },
      };
    }

    await createClient(config);

    // Exit the process successfully
    process.exit(0);
  })
  .catch((error) => {
    console.error(
      `ERROR: Failed to connect to the OpenAPI spec: ${error.message}`
    );
    console.error(
      `The URL to your Umbraco instance may be wrong or the instance is not running`
    );
    process.exit(1); // Exit with error
  });
