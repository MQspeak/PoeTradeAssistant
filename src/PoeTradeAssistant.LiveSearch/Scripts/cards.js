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
        const titleNodes = [...node.querySelectorAll('.itemName, [class*="itemName"], .item-name, .itemHeader .nameLine, .itemHeader .typeLine, .item-header .nameLine, .item-header .typeLine')];
        const title = titleNodes.flatMap(x => clean(x.innerText || x.textContent))[0]
            || clean(node.querySelector('.title, [class*="typeLine"]')?.innerText)[0]
            || clean(text)[0] || '未识别物品';
        const rarityNode = node.querySelector('[class*="rarity"], .itemHeader, .item-header');
        const rarityClasses = (rarityNode?.className || '') + ' ' + node.className + ' ' + [...node.querySelectorAll('[class*="rarity"]')].map(x => x.className).join(' ');
        const rarity = /unique|rarity3/i.test(rarityClasses) ? 'unique' : /rare|rarity2/i.test(rarityClasses) ? 'rare' : /magic|rarity1/i.test(rarityClasses) ? 'magic' : /gem|rarity4/i.test(rarityClasses) ? 'gem' : /currency|rarity5/i.test(rarityClasses) ? 'currency' : 'normal';
        let titleColor = ({unique:'#AF6025', rare:'#FFFF77', magic:'#8888FF', gem:'#1BA29B', currency:'#AA9E82', normal:'#C8C8C8'})[rarity];
        const actualTitleNode = titleNodes.find(x => clean(x.innerText || x.textContent).includes(title));
        const rgb = actualTitleNode && getComputedStyle(actualTitleNode).color.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)/);
        if (rarity === 'normal' && rgb && rgb.slice(1,4).some(x => Number(x) !== 0))
            titleColor = '#' + rgb.slice(1,4).map(x => Number(x).toString(16).padStart(2,'0')).join('');
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
