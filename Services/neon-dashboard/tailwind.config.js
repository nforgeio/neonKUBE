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
            'sans': ['"Pangram Sans"', ...defaultTheme.fontFamily.sans],
            'mono': [...defaultTheme.fontFamily.mono]
        },
        fontSize: {
            '0': 'var(--s0)',
            '1': 'var(--s1)',
            '2': 'var(--s2)',
            '3': 'var(--s3)',
            'base': 'var(--s3)',
            '4': 'var(--s4)',
            '5': 'var(--s5)',
            '6': 'var(--s6)',
            '7': 'var(--s7)',
            '8': 'var(--s8)',
            '9': 'var(--s9)'
        },
        spacing: {
            '0': 'var(--s0)',
            '1': 'var(--s1)',
            '2': 'var(--s2)',
            '3': 'var(--s3)',
            '4': 'var(--s4)',
            '5': 'var(--s5)',
            '6': 'var(--s6)',
            '7': 'var(--s7)',
            '8': 'var(--s8)',
            '9': 'var(--s9)'
        },
        borderRadius: {
            '0': 'var(--s0)',
            '1': 'var(--s1)',
            '2': 'var(--s2)',
            '3': 'var(--s3)',
            DEFAULT: 'var(--s3)',
            '4': 'var(--s4)',
            '5': 'var(--s5)',
            '6': 'var(--s6)',
            '7': 'var(--s7)',
            '8': 'var(--s8)',
            '9': 'var(--s9)'
        },
        extend: {},
    },

    plugins: [],
}