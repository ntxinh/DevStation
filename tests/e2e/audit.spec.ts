import { expect, test } from "@playwright/test";
import { seedProject, seedToken } from "./helpers";

test.describe("audit log", () => {
  test("shows seeded actions", async ({ page, request }) => {
    const name = `e2e-audit-${Date.now()}`;
    await seedProject(request, name); // writes a project.create AuditEvent
    await seedToken(page);

    await page.goto("/audit");
    await expect(
      page.getByRole("heading", { name: "Audit Log" }),
    ).toBeVisible();

    await page.getByLabel("Filter by action").fill("project.create");
    await expect(
      page.getByRole("cell", { name: "project.create" }).first(),
    ).toBeVisible();
  });
});
