/** @type {import('tailwindcss').Config} */
module.exports = {
    darkMode: ["class"],
    content: [
        "./**/*.{razor,html,cshtml}",
        "./Components/**/*.{razor,html}",
        "./Components/UI/**/*.{razor,html}",
        "./Components/Pages/**/*.{razor,html}",
        "./Pages/**/*.{razor,html,cshtml}",
    ],
    prefix: "",
    safelist: [
        "dark", "theme-shieldos", "theme-ironman", "theme-tech", "theme-future",
        {
            pattern: /text-status-(idle|charge|discharging|pause|continue|interrupt|error|msg|offline)/,
        },
    ],
    theme: {
        container: {
            center: true,
            padding: "2rem",
            screens: {
                "2xl": "1400px",
            },
        },
        extend: {
            colors: {
                border: "hsl(var(--border))",
                input: "hsl(var(--input))",
                ring: "hsl(var(--ring))",
                background: "hsl(var(--background))",
                foreground: "hsl(var(--foreground))",
                primary: {
                    DEFAULT: "hsl(var(--primary))",
                    foreground: "hsl(var(--primary-foreground))",
                },
                secondary: {
                    DEFAULT: "hsl(var(--secondary))",
                    foreground: "hsl(var(--secondary-foreground))",
                },
                destructive: {
                    DEFAULT: "hsl(var(--destructive))",
                    foreground: "hsl(var(--destructive-foreground))",
                },
                muted: {
                    DEFAULT: "hsl(var(--muted))",
                    foreground: "hsl(var(--muted-foreground))",
                },
                accent: {
                    DEFAULT: "hsl(var(--accent))",
                    foreground: "hsl(var(--accent-foreground))",
                },
                popover: {
                    DEFAULT: "hsl(var(--popover))",
                    foreground: "hsl(var(--popover-foreground))",
                },
                card: {
                    DEFAULT: "hsl(var(--card))",
                    foreground: "hsl(var(--card-foreground))",
                },
                // Additional status colors
                success: "hsl(var(--success))",
                warning: "hsl(var(--warning))",
                critical: "hsl(var(--critical))",
                // Battery status colors
                status: {
                    idle: "hsl(var(--status-idle))",
                    charge: "hsl(var(--status-charge))",
                    discharging: "hsl(var(--status-discharging))",
                    pause: "hsl(var(--status-pause))",
                    continue: "hsl(var(--status-continue))",
                    interrupt: "hsl(var(--status-interrupt))",
                    error: "hsl(var(--status-error))",
                    msg: "hsl(var(--status-msg))",
                    offline: "hsl(var(--status-offline))"
                },
                // Sidebar colors
                sidebar: {
                    DEFAULT: "hsl(var(--sidebar-background))",
                    foreground: "hsl(var(--sidebar-foreground))",
                    primary: "hsl(var(--sidebar-primary))",
                    "primary-foreground": "hsl(var(--sidebar-primary-foreground))",
                    accent: "hsl(var(--sidebar-accent))",
                    "accent-foreground": "hsl(var(--sidebar-accent-foreground))",
                    border: "hsl(var(--sidebar-border))",
                    ring: "hsl(var(--sidebar-ring))",
                },
                grid: {
                    header: "hsl(var(--grid-header))",
                    row: "hsl(var(--grid-row))",
                    "row-alt": "hsl(var(--grid-row-alt))",
                    "row-hover": "hsl(var(--grid-row-hover))",
                    "row-selected": "hsl(var(--grid-row-selected))",
                    border: "hsl(var(--grid-border))",
                },
                valid: "hsl(var(--valid))",
                invalid: "hsl(var(--invalid))",
            },
            borderRadius: {
                lg: "var(--radius)",
                md: "calc(var(--radius) - 2px)",
                sm: "calc(var(--radius) - 4px)",
            },
            keyframes: {
                "accordion-down": {
                    from: { height: "0" },
                    to: { height: "var(--radix-accordion-content-height)" },
                },
                "accordion-up": {
                    from: { height: "var(--radix-accordion-content-height)" },
                    to: { height: "0" },
                },
                "caret-blink": {
                    "0%,70%,100%": { opacity: "1" },
                    "20%,50%": { opacity: "0" },
                },
                "neon-pulse": {
                    "0%, 100%": {
                        boxShadow: "0 0 20px hsl(var(--primary) / 0.5), 0 0 40px hsl(var(--primary) / 0.3)",
                    },
                    "50%": {
                        boxShadow: "0 0 30px hsl(var(--primary) / 0.7), 0 0 60px hsl(var(--primary) / 0.5)",
                    },
                },
                "flicker": {
                    "0%, 100%": { opacity: "1" },
                    "50%": { opacity: "0.95" },
                },
                "marquee": {
                    "0%": { transform: "translateX(100%)" },
                    "100%": { transform: "translateX(-100%)" },
                },
            },
            animation: {
                "accordion-down": "accordion-down 0.2s ease-out",
                "accordion-up": "accordion-up 0.2s ease-out",
                "caret-blink": "caret-blink 1.25s ease-out infinite",
                "neon-pulse": "neon-pulse 2s ease-in-out infinite",
                "flicker": "flicker 0.15s infinite alternate",
                "marquee": "marquee 10s linear infinite",
            },
        },
    },
    plugins: [require("tailwindcss-animate")],
};
