const defaultTheme = require('tailwindcss/defaultTheme')


module.exports = {
    content: ["**/*.razor", "**/*.cshtml", "**/*.html"],

    theme: {
        colors: {
            transparent: 'transparent',
            current: 'currentColor',
            'light': 'var(--light)',
            'dark': 'var(--dark)',
            'link': 'var(--link)'
        },
        fontFamily: {
            'sans': ['Pangram Sans', ...defaultTheme.fontFamily.sans],
            'mono': [...defaultTheme.fontFamily.mono]
        },
        fontSize: {
            '0': 'var(--f0)',
            '1': 'var(--f1)',
            '2': 'var(--f2)',
            '3': 'var(--f3)',
            'base': 'var(--f3)',
            '4': 'var(--f4)',
            '5': 'var(--f5)',
            '6': 'var(--f6)',
            '7': 'var(--f7)',
            '8': 'var(--f8)',
            '9': 'var(--f9)'
        },
        spacing: {
            '0': 'var(--f0)',
            '1': 'var(--f1)',
            '2': 'var(--f2)',
            '3': 'var(--f3)',
            '4': 'var(--f4)',
            '5': 'var(--f5)',
            '6': 'var(--f6)',
            '7': 'var(--f7)',
            '8': 'var(--f8)',
            '9': 'var(--f9)'
        },
        borderRadius: {
            '0': 'var(--f0)',
            '1': 'var(--f1)',
            '2': 'var(--f2)',
            '3': 'var(--f3)',
            DEFAULT: 'var(--f3)',
            '4': 'var(--f4)',
            '5': 'var(--f5)',
            '6': 'var(--f6)',
            '7': 'var(--f7)',
            '8': 'var(--f8)',
            '9': 'var(--f9)'
        },
        extend: {},
    },

    plugins: [],
}