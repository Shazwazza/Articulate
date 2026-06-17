import { createClient, defaultPlugins } from "@hey-api/openapi-ts";
import chalk from "chalk";
import fetch from "node-fetch";

// Start notifying user we are generating the TypeScript client
console.log(chalk.green("Generating OpenAPI client..."));

const args = process.argv.slice(2);
const swaggerUrl = args[0];
const outputPath = args[1];

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
  console.error(chalk.red(`ERROR: Missing URL to OpenAPI spec or output path`));
  console.error(
    `Please provide the URL and output path as the first two arguments.`
  );
  console.error(
    `Example: node generate-openapi.js ${chalk.yellow(
      "https://.../swagger.json"
    )} ${chalk.yellow("./src/api")}`
  );
  process.exit(1);
}

// Start checking to see if we can connect to the OpenAPI spec
console.log("Ensure your Umbraco instance is running");
console.log(`Fetching OpenAPI definition from ${chalk.yellow(swaggerUrl)}`);

fetch(swaggerUrl)
  .then(async (response) => {
    if (!response.ok) {
      console.error(
        chalk.red(
          `ERROR: OpenAPI spec returned with a non OK (200) response: ${response.status} ${response.statusText}`
        )
      );
      console.error(
        `The URL to your Umbraco instance may be wrong or the instance is not running`
      );
      console.error(
        `Please verify or change the URL in the ${chalk.yellow(
          "package.json"
        )} for the script ${chalk.yellow("generate-openapi")}`
      );
      console.error(
        `Or review back office logs, ${chalk.yellow(
          "Swagger"
        )} may not be able to generate a valid schema due to ${chalk.yellow(
          "route conflicts or duplicate API attributes"
        )}; (e.g. multiple GET methods for the same route, or decorating methods with '[ProducesResponseType(StatusCodes.Status401Unauthorized)]' which Swagger already does).`
      );
      process.exit(1);
    }

    console.log(`OpenAPI spec fetched successfully`);
    console.log(
      `Calling ${chalk.yellow("hey-api")} to generate TypeScript client`
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
        ...defaultPlugins,
        '@hey-api/client-fetch',
        {
          name: '@hey-api/sdk',
          asClass: true,
          classNameBuilder: '{{name}}Service',
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
      `ERROR: Failed to connect to the OpenAPI spec: ${chalk.red(
        error.message
      )}`
    );
    console.error(
      `The URL to your Umbraco instance may be wrong or the instance is not running`
    );
    process.exit(1); // Exit with error
  });
