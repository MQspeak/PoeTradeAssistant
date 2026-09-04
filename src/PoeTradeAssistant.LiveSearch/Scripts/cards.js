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
        const title = node.querySelector('.itemName, [class*="itemName"], .item-name')?.textContent?.trim()
            || node.querySelector('.title, [class*="typeLine"]')?.textContent?.trim()
            || text.split('\n').find(x => x.trim()) || '未识别物品';
        const price = text.match(/(?:Asking Price|Exact Price|询价|售價|售价)[:：]?\s*([\s\S]*?)(?:\s+Fee:|\s+费用|\s+listed\b|\s+上架|\s+Travel to Hideout|\s+前往藏身|\s+Ignore Player|$)/i)?.[1]?.trim()
            || node.querySelector('[class*="price"], .currency')?.textContent?.trim() || '';
        const action = [...node.querySelectorAll('button, a, [role="button"]')].find(x =>
            /travel to hideout|前往藏身[处處]/i.test(x.textContent || '') || x.classList.contains('direct-btn'));
        return { id, title, price, detail: text.slice(0, 16000),
            url: location.href.split('#')[0] + '#' + encodeURIComponent(id),
            canTravel: !!action && !action.disabled && action.getAttribute('aria-disabled') !== 'true' };
    }).filter(Boolean).slice(0, 60);
}
