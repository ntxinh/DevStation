import { expect, test } from "@playwright/test";
import { seedDbConnection, seedToken } from "./helpers";

test.describe("databases", () => {
  test("safe query: SELECT 1 returns a row with timing", async ({
    page,
    request,
  }) => {
    const name = `e2e-pg-${Date.now()}`;
    await seedDbConnection(request, name);
    await seedToken(page);

    await page.goto("/databases");
    await expect(
      page.getByRole("heading", { name: "Databases" }),
    ).toBeVisible();

    await page.getByRole("button", { name: name }).click();
    const editor = page.getByPlaceholder(/SELECT/);
    await editor.fill("SELECT 1 AS one");
    await page.getByRole("button", { name: /Run/ }).click();

    // result grid: header + data row + "N rows · N ms" summary
    await expect(page.getByRole("columnheader", { name: "one" })).toBeVisible({
      timeout: 15_000,
    });
    await expect(page.getByText("1", { exact: true }).first()).toBeVisible();
    await expect(page.getByText(/\d+ rows · \d+ ms/)).toBeVisible();
  });
});
