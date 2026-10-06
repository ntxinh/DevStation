import { expect, test } from "@playwright/test";
import { seedToken } from "./helpers";

test.describe("command palette", () => {
  test("Cmd+K opens the palette and navigates", async ({ page }) => {
    await seedToken(page);
    await page.goto("/dashboard");

    await page.keyboard.press("Control+k");
    const input = page.getByPlaceholder("Type a command or search…");
    await expect(input).toBeVisible();

    // command list is served by /api/commands; pick a nav command
    await input.fill("jobs");
    const item = page.getByRole("button", { name: /Go to Jobs/ });
    await expect(item).toBeVisible();
    await item.click();

    await page.waitForURL(/\/jobs$/);
    await expect(page.getByRole("heading", { name: "Jobs" })).toBeVisible();

    // escape closes the palette
    await page.keyboard.press("Control+k");
    await expect(input).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(input).not.toBeVisible();
  });
});
