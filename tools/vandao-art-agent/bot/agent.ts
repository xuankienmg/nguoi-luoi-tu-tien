import { fileURLToPath } from "node:url";
import { defineAgent } from "@grok/agent-sdk";

export default defineAgent({
  local: { workspaceDir: fileURLToPath(new URL("../../../", import.meta.url)), sandbox: true },
  model: {
    id: "grok-4.7",
    params: [
      { id: "reasoning_effort", value: "high" },
      { id: "fast", value: "true" },
    ],
  },
});

