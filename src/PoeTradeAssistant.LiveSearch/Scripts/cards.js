// Adapted from poe2-live-search-manager's DOM extraction rules.
// One extractor is used for baseline, updates, details and action identity.
() => {
    const roots = [...document.querySelectorAll('.resultset [data-id], .search-results [data-id], .resultset .row, .search-results .row')];
    const seen = new Set();
    return roots.map(node => {
        if (node.parentElement?.closest('[data-id]')) return null;
        const id = node.getAttribute('data-id');
        if (!id || seen.has(id)) return null;
        seen.add(id);
        const text = node.innerText || '';
        const clean = value => (value || '').split('\n').map(x => x.trim()).filter(x => x && !/^(verified|已验证|已驗證)$/i.test(x));
        // Keep the actual text element alongside the title, including fallback paths.
        // The trade header may contain a Verified badge and nested coloured spans.
        const ignored = 'button, [role="button"], [class*="price"], [class*="account"], [class*="seller"], .verified';
        const visible = el => {
            const css = getComputedStyle(el);
            return css.display !== 'none' && css.visibility !== 'hidden';
        };
        const candidates = root => {
            const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
            const found = [];
            while (walker.nextNode()) {
                const el = walker.currentNode.parentElement;
                if (!el || el.closest(ignored) || !visible(el)) continue;
                const value = clean(walker.currentNode.textContent)[0];
                if (value) found.push({ title: value, element: el });
            }
            return found;
        };
        let picked;
        for (const selector of ['.itemName, [class*="itemName"], .item-name', '.nameLine, .name-line', '.typeLine, .type-line', '.itemHeader, .item-header, .itemNameWrapper', '.title']) {
            for (const root of node.querySelectorAll(selector)) {
                picked = candidates(root)[0];
                if (picked) break;
            }
            if (picked) break;
        }
        picked ||= candidates(node)[0];
        const title = picked?.title || '未识别物品';
        const titleElement = picked?.element;
        // Read rarity only from the name's ancestors, never unrelated card controls.
        const classes = [];
        for (let el = titleElement; el && node.contains(el); el = el.parentElement)
            classes.push(typeof el.className === 'string' ? el.className : '');
        const rarityClasses = classes.join(' ');
        const rarity = /unique|rarity3/i.test(rarityClasses) ? 'unique' : /rare|rarity2/i.test(rarityClasses) ? 'rare' : /magic|rarity1/i.test(rarityClasses) ? 'magic' : /gem|rarity4/i.test(rarityClasses) ? 'gem' : /currency|rarity5/i.test(rarityClasses) ? 'currency' : 'normal';
        let titleColor = ({unique:'#AF6025', rare:'#FFFF77', magic:'#8888FF', gem:'#1BA29B', currency:'#AA9E82', normal:'#C8C8C8'})[rarity];
        if (titleElement) {
            const color = getComputedStyle(titleElement).color;
            // Canvas normalizes CSS colour formats (including color(srgb ...)) to RGBA.
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = 1;
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = color;
            ctx.fillRect(0, 0, 1, 1);
            const rgba = ctx.getImageData(0, 0, 1, 1).data;
            if (rgba[3] && (rgba[0] || rgba[1] || rgba[2]))
                titleColor = '#' + [...rgba.slice(0, 3)].map(x => x.toString(16).padStart(2, '0')).join('');
        }
        const rawPrice = text.match(/(?:Asking Price|Exact Price|询价|售價|售价)[:：]?\s*([\s\S]*?)(?:\s+Fee:|\s+费用|\s+listed\b|\s+上架|\s+Travel to Hideout|\s+前往藏身|\s+Ignore Player|$)/i)?.[1]
            || node.querySelector('[class*="price"], .currency')?.textContent || '';
        const price = rawPrice.replace(/\s*[^\s#]+#\d+.*$/s, '').replace(/\s+(?:Online|Offline|Direct Whisper).*$/is, '').trim();
        const action = [...node.querySelectorAll('button, a, [role="button"]')].find(x =>
            /travel to hideout|前往藏身[处處]/i.test(x.textContent || '') || x.classList.contains('direct-btn'));
        return { id, title, titleColor, price, detail: text.slice(0, 16000),
            url: location.href.split('#')[0] + '#' + encodeURIComponent(id),
            canTravel: !!action && !action.disabled && action.getAttribute('aria-disabled') !== 'true' };
    }).filter(Boolean).slice(0, 60);
}
