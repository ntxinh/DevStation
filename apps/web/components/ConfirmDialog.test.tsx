import { it, expect, vi, afterEach } from "vitest";
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { ConfirmDialog } from "./ConfirmDialog";

declare global {
  var IS_REACT_ACT_ENVIRONMENT: boolean;
}

globalThis.IS_REACT_ACT_ENVIRONMENT = true;

let host: HTMLDivElement | null = null;
let root: Root | null = null;

function render(el: React.ReactElement) {
  host = document.createElement("div");
  document.body.appendChild(host);
  act(() => {
    root = createRoot(host!);
    root.render(el);
  });
  return host;
}

function type(input: HTMLInputElement, value: string) {
  act(() => {
    const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
    setter.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  });
}

function confirmBtn() {
  return Array.from(host!.querySelectorAll("button")).find((b) => b.textContent === "Confirm")!;
}

afterEach(() => {
  if (root) act(() => root!.unmount());
  host?.remove();
  host = null;
  root = null;
});

it("requireText keeps confirm disabled until the name is typed", () => {
  const onConfirm = vi.fn();
  render(
    <ConfirmDialog
      open
      title="Delete project"
      description="This cannot be undone."
      requireText="alpha"
      onConfirm={onConfirm}
      onCancel={() => {}}
    />,
  );
  expect(confirmBtn().disabled).toBe(true);
  const input = host!.querySelector("input")!;
  type(input, "beta");
  expect(confirmBtn().disabled).toBe(true);
  type(input, "alpha");
  expect(confirmBtn().disabled).toBe(false);
  act(() => confirmBtn().click());
  expect(onConfirm).toHaveBeenCalledOnce();
});

it("confirms immediately when no requireText", () => {
  const onConfirm = vi.fn();
  render(<ConfirmDialog open title="Restart" description="Are you sure?" onConfirm={onConfirm} onCancel={() => {}} />);
  expect(host!.querySelector("input")).toBeNull();
  expect(confirmBtn().disabled).toBe(false);
  act(() => confirmBtn().click());
  expect(onConfirm).toHaveBeenCalledOnce();
});

it("Esc calls onCancel and the dialog is a modal dialog", () => {
  const onCancel = vi.fn();
  render(<ConfirmDialog open title="T" description="D" onConfirm={() => {}} onCancel={onCancel} />);
  expect(host!.querySelector('[role="dialog"]')?.getAttribute("aria-modal")).toBe("true");
  act(() => {
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
  });
  expect(onCancel).toHaveBeenCalledOnce();
});

it("renders nothing when closed", () => {
  render(<ConfirmDialog open={false} title="T" description="D" onConfirm={() => {}} onCancel={() => {}} />);
  expect(host!.querySelector('[role="dialog"]')).toBeNull();
});
