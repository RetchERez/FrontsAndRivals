"""Fronts & Rivals: builds the gang wardrobe extras (hats, glasses, chains, ties, watches, attack masks) into
src/Content/outfits.json from the game's own clothing names (github.com/root-cause/v-clothingnames).

Every piece is picked by its exact in-game name ("Dark Green Wool Beanie", "Gold Chain"...), never by a guessed number,
so the file can be rebuilt any time:

    python tools/OutfitBuilder/build_extras.py <folder with the v-clothingnames json files>

Missing files are downloaded into that folder first. In the game, every piece is still checked against the player's
copy before anyone wears it (Wardrobe.SelfCheck), so a piece a game version lacks is simply skipped.
"""
import io, json, os, sys, urllib.request

SRC = 'https://raw.githubusercontent.com/root-cause/v-clothingnames/master/'
FILES = {
    ('hat', 'm'): 'props_male_hats', ('hat', 'f'): 'props_female_hats',
    ('glasses', 'm'): 'props_male_glasses', ('glasses', 'f'): 'props_female_glasses',
    ('watch', 'm'): 'props_male_watches', ('watch', 'f'): 'props_female_watches',
    ('neck', 'm'): 'male_accessories', ('neck', 'f'): 'female_accessories',
    ('mask', 'm'): 'masks_male', ('mask', 'f'): 'masks_female',
}

# The mod's colour keys and the words the game's clothing names use for them, best match first.
COLOUR_WORDS = {
    'white': ['White'], 'red': ['Red'], 'orange': ['Orange'], 'yellow': ['Yellow'], 'green': ['Green'],
    'forest': ['Dark Green', 'Forest', 'Green'], 'teal': ['Teal'], 'lightblue': ['Sky Blue', 'Light Blue'],
    'blue': ['Royal Blue', 'Blue'], 'purple': ['Purple'], 'pink': ['Pink'], 'burgundy': ['Burgundy'], 'gold': ['Gold'],
    'cyan': ['Cyan'], 'lime': ['Lime'], 'lavender': ['Lavender'], 'brightblue': ['Blue', 'Royal Blue'],
    'black': ['Black'], 'grey': ['Gray', 'Grey'],
}


def load(folder, name):
    path = os.path.join(folder, name + '.json')
    if not os.path.exists(path):
        urllib.request.urlretrieve(SRC + name + '.json', path)
    return json.load(io.open(path, encoding='utf-8'))


def by_name(data, exact):
    """(drawable, texture) of the piece with exactly this name, or None."""
    for d, tex in data.items():
        for t, v in tex.items():
            if (v.get('Localized') or '') == exact:
                return int(d), int(t)
    return None


def coloured(data, type_name, fallback=None):
    """One drawable of '<colour> <type_name>' with a texture per colour key; the drawable with the most colours wins.
    fallback: a colour key whose texture stands in for colours the piece doesn't come in (e.g. black masks)."""
    best = None
    for d, tex in data.items():
        found = {}
        for key, words in COLOUR_WORDS.items():
            for w in words:
                hit = [int(t) for t, v in tex.items() if (v.get('Localized') or '') == w + ' ' + type_name]
                if hit:
                    found[key] = hit[0]
                    break
        if found and (best is None or len(found) > len(best[1])):
            best = (int(d), found)
    if best is None:
        return None
    piece = {'d': best[0], 't': best[1]}
    if fallback and fallback in best[1]:
        piece['t0'] = best[1][fallback]
        piece['fallback'] = True
    return piece


def fixed(data, *names):
    out = []
    for n in names:
        hit = by_name(data, n)
        if hit:
            out.append({'d': hit[0], 't0': hit[1]})
    return out


def main():
    folder = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), 'cache')
    os.makedirs(folder, exist_ok=True)
    D = {k: load(folder, v) for k, v in FILES.items()}

    def both(fn):
        return {'m': fn('m'), 'f': fn('f')}

    beanie = both(lambda g: [coloured(D[('hat', g)], 'Wool Beanie')])
    shades_biker = both(lambda g: fixed(D[('glasses', g)], 'Black Wraparounds', 'Black Winter Shades') if g == 'm'
                        else fixed(D[('glasses', g)], 'Black Aviators'))
    shades_smart = both(lambda g: fixed(D[('glasses', g)], 'Gold Aviators', 'Black Summer Shades') if g == 'm'
                        else fixed(D[('glasses', g)], 'Gold Aviators', 'Black Aviators'))
    chain = both(lambda g: fixed(D[('neck', g)], 'Gold Chain', 'Silver Chain'))
    gold_chain = both(lambda g: fixed(D[('neck', g)], 'Gold Chain'))
    watch = both(lambda g: fixed(D[('watch', g)], 'Gold Watch') if g == 'm' else fixed(D[('watch', g)], 'Gold Fashion'))
    tie = both(lambda g: [coloured(D[('neck', g)], 'Tie')] if g == 'm' else [coloured(D[('neck', g)], 'Skinny Tie')])
    mask_colour = both(lambda g: [coloured(D[('mask', g)], 'Face Bandana', fallback='black')])

    def clean(items):
        return {g: [x for x in v if x] for g, v in items.items()}

    def piece(slot, chance, items, only=None, when=None):
        p = {'slot': slot, 'chance': chance}
        if only: p['only'] = only    # under-layer the look needs (a tie needs a shirt collar)
        if when: p['when'] = when    # 'attack': only on people hitting your fronts
        p.update(clean(items))
        return p

    extras = {
        # Dead Kings: street. A green beanie, chains, and green bandanas pulled up when they come for your fronts.
        'CK': [piece('hat', 0.45, beanie), piece('neck', 0.35, chain), piece('mask', 1.0, mask_colour, when='attack')],
        # Hellfire MC: bikers. An orange beanie, black wraparounds, black bandanas for a hit.
        'IR': [piece('hat', 0.35, beanie), piece('glasses', 0.5, shades_biker), piece('mask', 1.0, mask_colour, when='attack')],
        # Vinewood Elite: suits. A royal blue tie with every shirt, aviators. No masks: they don't hide.
        'VS': [piece('neck', 1.0, tie, only='shirt'), piece('glasses', 0.5, shades_smart)],
        # Your crew, in your colour.
        'player_street': [piece('hat', 0.4, beanie), piece('neck', 0.3, chain)],
        'player_biker': [piece('hat', 0.35, beanie), piece('glasses', 0.5, shades_biker)],
        'player_smart': [piece('neck', 1.0, tie, only='shirt'), piece('glasses', 0.4, shades_smart)],
        'player_mixed': [piece('hat', 0.3, beanie), piece('glasses', 0.3, shades_smart), piece('neck', 0.2, chain)],
        # Bosses and lieutenants: a step up, so you can pick them out in a fight.
        'lead': [piece('neck', 1.0, gold_chain), piece('watch', 1.0, watch), piece('glasses', 1.0, shades_smart)],
    }

    path = os.path.join(os.path.dirname(__file__), '..', '..', 'src', 'Content', 'outfits.json')
    data = json.load(io.open(path, encoding='utf-8'))
    data['extras'] = extras
    io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(data, indent=1, ensure_ascii=False) + '\n')
    for style, pieces in extras.items():
        print(style, [(p['slot'], {g: [(x['d'], len(x.get('t', {})) or 'fixed') for x in p[g]] for g in ('m', 'f')}) for p in pieces])


if __name__ == '__main__':
    main()
