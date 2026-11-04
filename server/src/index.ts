#!/usr/bin/env node
import { readFileSync } from "node:fs";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { BridgeClient } from "./bridge-client.js";
import { defaultDiscoveryPath } from "./discovery.js";
import { createServer } from "./server.js";

const manifest = JSON.parse(
  readFileSync(new URL("../package.json", import.meta.url), "utf8"),
) as { version: string };

const server = createServer(new BridgeClient(defaultDiscoveryPath()), manifest.version);
await server.connect(new StdioServerTransport());
