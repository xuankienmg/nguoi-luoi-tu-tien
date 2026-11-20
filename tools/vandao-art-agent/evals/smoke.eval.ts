import { defineEval, includes } from "@grok/agent-sdk/evals";

export default defineEval({
  tags: ["smoke"],
  description: "The VanDao agent inspects the Trúc Cơ idle atlas before reporting grid geometry.",
  async test(t) {
    await t.send("Kiểm tra PNG wwwroot/img/skin/inventory-male/bich-van-dao-bao-nam-idle-64-v1.png. Sheet có đúng 8×8 ô 384×512 không? Báo kích thước và số khung.");
    t.succeeded();
    t.calledTool("inspect_png");
    t.check(t.reply, includes(/64/));
  },
});

