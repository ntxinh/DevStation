import { expect, test } from "@playwright/test";
import { seedToken } from "./helpers";

test.describe("projects", () => {
  test("create → list → detail renders the git card region", async ({
    page,
  }) => {
    await seedToken(page);
    const name = `e2e-project-${Date.now()}`;

    await page.goto("/projects");
    await expect(page.getByRole("heading", { name: "Projects" })).toBeVisible();

    // create
    await page.getByRole("button", { name: "New project" }).click();
    const dialog = page.getByRole("dialog", { name: "New project" });
    await expect(dialog).toBeVisible();
    await dialog.getByLabel("Name").fill(name);
    await dialog.getByLabel("Technology").fill("dotnet");
    await dialog.getByRole("button", { name: "Create", exact: true }).click();

    // create redirects straight to the detail page
    await page.waitForURL(/\/projects\//);
    await expect(
      page.getByRole("heading", { name, exact: true }),
    ).toBeVisible();

    // the git card renders (real status, or its unavailable placeholder when
    // the project has no local repo on this host)
    const gitState = page
      .getByText("Git status unavailable")
      .or(page.getByText("Ahead / behind"));
    await expect(gitState.first()).toBeVisible();

    // back to the list — the project row is there
    await page.getByRole("link", { name: "← Projects" }).click();
    await expect(page.getByRole("link", { name })).toBeVisible();
  });
});
