import { expect, test } from "@playwright/test";
import { seedToken, TOKEN } from "./helpers";

test.describe("dashboard", () => {
  test("token gate accepts the dev token", async ({ page }) => {
    await page.goto("/dashboard");
    await expect(page.getByPlaceholder("dev token")).toBeVisible();
    await page.getByPlaceholder("dev token").fill(TOKEN);
    await page.getByRole("button", { name: "Connect" }).click();
    await expect(
      page.getByRole("heading", { name: "Dashboard" }),
    ).toBeVisible();
  });

  test("renders nav and stats", async ({ page }) => {
    await seedToken(page);
    await page.goto("/dashboard");

    // sidebar nav (spec order)
    for (const label of [
      "Dashboard",
      "Projects",
      "Environments",
      "Services",
      "Containers",
      "Databases",
      "Jobs",
      "Deployments",
      "Logs",
      "Health",
    ]) {
      await expect(
        page.getByRole("link", { name: label, exact: true }),
      ).toBeVisible();
    }

    await expect(
      page.getByRole("heading", { name: "Dashboard" }),
    ).toBeVisible();
    const main = page.getByRole("main");
    for (const stat of [
      "Projects",
      "Services",
      "Containers",
      "Environment health",
    ]) {
      await expect(main.getByText(stat, { exact: true })).toBeVisible();
    }
  });
});
