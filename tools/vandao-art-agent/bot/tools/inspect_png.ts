import { open, realpath } from "node:fs/promises";
import { isAbsolute, relative, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { defineTool } from "@grok/agent-sdk/tools";
import { z } from "zod";

const checkout = fileURLToPath(new URL("../../../../", import.meta.url));
const dimension = z.number().int().min(1).max(32768);

export default defineTool({
  description: "Read PNG header metadata from a local VanDao asset and check its optional sprite grid.",
  execution: "server",
  effect: "read",
  inputSchema: z.object({
    path: z.string().min(1),
    grid: z.object({
      columns: dimension,
      rows: dimension,
      frameWidth: dimension,
      frameHeight: dimension,
    }).optional(),
  }),
  async execute({ path, grid }) {
    if (isAbsolute(path) || path.includes(":")) {
      throw new Error("Use a VanDao-relative PNG path.");
    }
    const candidate = resolve(checkout, path);
    const withinAssetFolder = (value: string, root: string) => {
      const rel = relative(root, value);
      return rel.startsWith("wwwroot" + sep + "img" + sep)
        || rel.startsWith("output" + sep);
    };
    if (!withinAssetFolder(candidate, checkout) || !candidate.toLowerCase().endsWith(".png")) {
      throw new Error("Use a PNG under wwwroot/img or output.");
    }
    const [actual, root] = await Promise.all([realpath(candidate), realpath(checkout)]);
    if (!withinAssetFolder(actual, root) || !actual.toLowerCase().endsWith(".png")) {
      throw new Error("The PNG resolves outside the asset folders.");
    }

    const file = await open(actual, "r");
    try {
      const header = Buffer.alloc(33);
      const { bytesRead } = await file.read(header, 0, header.length, 0);
      if (bytesRead !== header.length || header.subarray(0, 8).toString("hex") !== "89504e470d0a1a0a"
          || header.readUInt32BE(8) !== 13 || header.toString("ascii", 12, 16) !== "IHDR") {
        throw new Error("Invalid PNG header.");
      }
      const width = header.readUInt32BE(16);
      const height = header.readUInt32BE(20);
      const colorType = header[25];
      if (!width || !height || ![0, 2, 3, 4, 6].includes(colorType)) {
        throw new Error("Invalid PNG dimensions or color type.");
      }
      return {
        path: relative(root, actual).split(sep).join("/"),
        width,
        height,
        bitDepth: header[24],
        colorType,
        hasAlphaChannel: colorType === 4 || colorType === 6,
        grid: grid ? {
          ...grid,
          frameCount: grid.columns * grid.rows,
          matches: width === grid.columns * grid.frameWidth && height === grid.rows * grid.frameHeight,
        } : null,
      };
    } finally {
      await file.close();
    }
  },
});

