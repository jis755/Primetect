import type { Config } from "tailwindcss";

const config: Config = {
  content: ["./app/**/*.{js,ts,jsx,tsx,mdx}", "./components/**/*.{js,ts,jsx,tsx,mdx}"],
  theme: {
    extend: {
      colors: {
        ink: "#10231f",
        pine: "#174c40",
        mint: "#dff3e9",
        sand: "#f6f2e8",
        sun: "#e8b65a",
      },
      boxShadow: {
        soft: "0 22px 60px rgba(16, 35, 31, 0.10)",
      },
    },
  },
  plugins: [],
};

export default config;
