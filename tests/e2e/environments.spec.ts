import { expect, test } from "@playwright/test";
import { seedToken } from "./helpers";

test.describe("environments + containers", () => {
  test("environments list renders", async ({ page }) => {
    await seedToken(page);
    await page.goto("/environments");
    await expect(
      page.getByRole("heading", { name: "Environments" }),
    ).toBeVisible();
    // either the table or the empty state — both are valid render outcomes
    const state = page
      .getByRole("columnheader", { name: "Name" })
      .or(page.getByText("No environments yet"));
    await expect(state.first()).toBeVisible();
  });

  test("containers page renders (podman may be absent)", async ({ page }) => {
    await seedToken(page);
    await page.goto("/containers");
    await expect(
      page.getByRole("heading", { name: "Containers" }),
    ).toBeVisible();
    // table, empty state, or a provider error — the page must render either way
    const state = page
      .getByRole("columnheader", { name: "Name" })
      .or(page.getByText("No containers"))
      .or(page.getByText(/error|failed|unavailable/i));
    await expect(state.first()).toBeVisible();
  });
});
