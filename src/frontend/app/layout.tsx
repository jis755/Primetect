import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "PrimeTect | Property operations, made clear",
  description: "A calm operating system for property teams, tenants, and owners.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}
