import { test, expect } from '@playwright/test';

test.describe('UI Redesign — Design Tokens & Theme', () => {
  test('login page loads with light mode by default', async ({ page }) => {
    await page.goto('/login');

    // Page should have light background (bg-primary = #F8FAFC)
    const body = page.locator('body');
    await expect(body).toBeVisible();

    // The html element should NOT have .dark class by default
    const htmlClass = await page.locator('html').getAttribute('class');
    expect(htmlClass ?? '').not.toContain('dark');

    // MyMemo logo should be visible (use exact match to avoid Clerk's "Sign in to MyMemo")
    await expect(page.getByRole('heading', { name: 'MyMemo', exact: true })).toBeVisible();
  });

  test('login page uses correct background color', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    const bgColor = await page.evaluate(() => {
      return getComputedStyle(document.documentElement).getPropertyValue('--color-bg-primary').trim();
    });
    expect(bgColor.toLowerCase()).toBe('#f8fafc');
  });

  test('design tokens are defined as CSS custom properties', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    const tokens = await page.evaluate(() => {
      const style = getComputedStyle(document.documentElement);
      return {
        bgPrimary: style.getPropertyValue('--color-bg-primary').trim().toLowerCase(),
        bgCard: style.getPropertyValue('--color-bg-card').trim().toLowerCase(),
        textPrimary: style.getPropertyValue('--color-text-primary').trim().toLowerCase(),
        textSecondary: style.getPropertyValue('--color-text-secondary').trim().toLowerCase(),
        accent: style.getPropertyValue('--color-accent').trim().toLowerCase(),
        success: style.getPropertyValue('--color-success').trim().toLowerCase(),
        danger: style.getPropertyValue('--color-danger').trim().toLowerCase(),
        border: style.getPropertyValue('--color-border').trim().toLowerCase(),
      };
    });

    expect(tokens.bgPrimary).toBe('#f8fafc');
    // Browser may shorten #ffffff to #fff
    expect(['#ffffff', '#fff']).toContain(tokens.bgCard);
    expect(tokens.textPrimary).toBe('#0f172a');
    expect(tokens.textSecondary).toBe('#475569');
    expect(tokens.accent).toBe('#2563eb');
    expect(tokens.success).toBe('#059669');
    expect(tokens.danger).toBe('#dc2626');
    expect(tokens.border).toBe('#e2e8f0');
  });

  test('dark mode tokens are applied when .dark class is set', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    // Manually toggle dark mode
    await page.evaluate(() => {
      document.documentElement.classList.add('dark');
    });

    const tokens = await page.evaluate(() => {
      const style = getComputedStyle(document.documentElement);
      return {
        bgPrimary: style.getPropertyValue('--color-bg-primary').trim().toLowerCase(),
        bgCard: style.getPropertyValue('--color-bg-card').trim().toLowerCase(),
        textPrimary: style.getPropertyValue('--color-text-primary').trim().toLowerCase(),
        accent: style.getPropertyValue('--color-accent').trim().toLowerCase(),
      };
    });

    expect(tokens.bgPrimary).toBe('#0f172a');
    expect(tokens.bgCard).toBe('#1e293b');
    expect(tokens.textPrimary).toBe('#f8fafc');
    expect(tokens.accent).toBe('#3b82f6');
  });

  test('Inter font is loaded and applied to body', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    const fontFamily = await page.evaluate(() => {
      return getComputedStyle(document.body).fontFamily;
    });

    expect(fontFamily).toContain('Inter');
  });

  test('Poppins and Inter fonts are loaded in the document', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    // Check that font stylesheets are loaded by verifying @font-face declarations
    const fontsLoaded = await page.evaluate(() => {
      const sheets = Array.from(document.styleSheets);
      let hasPoppins = false;
      let hasInter = false;
      for (const sheet of sheets) {
        try {
          for (const rule of sheet.cssRules) {
            const text = rule.cssText.toLowerCase();
            if (text.includes('poppins')) hasPoppins = true;
            if (text.includes('inter')) hasInter = true;
          }
        } catch {
          // Cross-origin sheets will throw
        }
      }
      return { hasPoppins, hasInter };
    });

    expect(fontsLoaded.hasPoppins).toBe(true);
    expect(fontsLoaded.hasInter).toBe(true);
  });

  test('theme-color meta tag is set to professional blue', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    const themeColor = await page.evaluate(() => {
      const meta = document.querySelector('meta[name="theme-color"]');
      return meta?.getAttribute('content');
    });

    expect(themeColor).toBe('#2563EB');
  });

  test('no navy-* CSS custom properties remain', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    const navyTokens = await page.evaluate(() => {
      const style = getComputedStyle(document.documentElement);
      const results: string[] = [];
      for (const suffix of ['950', '900', '800', '700', '600']) {
        const val = style.getPropertyValue(`--color-navy-${suffix}`).trim();
        if (val) results.push(`--color-navy-${suffix}: ${val}`);
      }
      return results;
    });

    expect(navyTokens).toHaveLength(0);
  });
});

test.describe('UI Redesign — Login Page', () => {
  test('login page has centered layout with logo', async ({ page }) => {
    await page.goto('/login');

    // Logo heading should be visible
    const logo = page.getByRole('heading', { name: 'MyMemo', exact: true });
    await expect(logo).toBeVisible();

    // The page should have the accent color in "Memo" part
    const memoSpan = logo.locator('span.text-accent');
    await expect(memoSpan).toBeVisible();
    await expect(memoSpan).toHaveText('Memo');
  });

  test('login page background matches bg-primary token', async ({ page }) => {
    await page.goto('/login');
    await page.waitForLoadState('domcontentloaded');

    // The outermost div should use bg-bg-primary
    const container = page.locator('div.bg-bg-primary').first();
    await expect(container).toBeVisible();
  });
});

test.describe('UI Redesign — Layout (authenticated routes redirect)', () => {
  test('unauthenticated user is redirected to login', async ({ page }) => {
    await page.goto('/');
    await page.waitForLoadState('networkidle');

    // Should either be on /login or show MyMemo branding
    const url = page.url();
    const hasLogin = url.includes('login') || url.includes('#');
    const hasLogo = await page.getByRole('heading', { name: 'MyMemo', exact: true }).isVisible().catch(() => false);
    expect(hasLogin || hasLogo).toBe(true);
  });
});

test.describe('UI Redesign — Responsive', () => {
  test('login page renders correctly at mobile width (375px)', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 812 });
    await page.goto('/login');

    await expect(page.getByRole('heading', { name: 'MyMemo', exact: true })).toBeVisible();
  });

  test('login page renders correctly at tablet width (768px)', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 });
    await page.goto('/login');

    await expect(page.getByRole('heading', { name: 'MyMemo', exact: true })).toBeVisible();
  });

  test('login page renders correctly at desktop width (1440px)', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/login');

    await expect(page.getByRole('heading', { name: 'MyMemo', exact: true })).toBeVisible();
  });
});

test.describe('UI Redesign — Accessibility', () => {
  test('page has correct lang attribute', async ({ page }) => {
    await page.goto('/login');
    const lang = await page.locator('html').getAttribute('lang');
    expect(lang).toBe('en');
  });

  test('page title is set', async ({ page }) => {
    await page.goto('/login');
    await expect(page).toHaveTitle(/MyMemo/);
  });

  test('accent color is professional blue #2563EB', async ({ page }) => {
    await page.goto('/login');

    const accent = await page.evaluate(() => {
      return getComputedStyle(document.documentElement).getPropertyValue('--color-accent').trim().toLowerCase();
    });
    expect(accent).toBe('#2563eb');
  });
});
