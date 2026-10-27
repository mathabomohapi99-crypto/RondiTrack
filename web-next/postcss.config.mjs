// Tells Next.js to run Tailwind on the CSS (fixes the unstyled page and the @theme warning)
const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;
