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
    'forest': ['Dark Green', 'Forest', 'Green'], 'teal': ['Teal'], 'lightblue': ['Sky Blue', 'Light Blue', 'Baby Blue'],
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
    mask_skull = both(lambda g: fixed(D[('mask', g)], 'Skull Face Bandana'))
    skull_cap = both(lambda g: fixed(D[('hat', g)], 'Skull Cap'))
    trilby = both(lambda g: [coloured(D[('hat', g)], 'Trilby')] if g == 'm' else [])

    def clean(items):
        return {g: [x for x in v if x] for g, v in items.items()}

    open_collar = {'m': [], 'f': []}
    for g, f in (('m', 'male_tops'), ('f', 'female_tops')):
        tops = load(folder, f)
        open_collar[g] = sorted(int(k) for k, tex in tops.items() if any((v.get('Localized') or '').endswith('Revere Collar') for v in tex.values()))

    def piece(slot, chance, items, only=None, when=None, tops=None):
        p = {'slot': slot, 'chance': chance}
        if tops: p['tops'] = tops    # only with these tops (a chain shows on an open collar, clips through a hoodie)
        if only: p['only'] = only    # under-layer the look needs (a tie needs a shirt collar)
        if when: p['when'] = when    # 'attack': only on people hitting your fronts
        p.update(clean(items))
        return p

    extras = {
        # Each crew looks like nobody else:
        # Dead Kings: street. A green beanie, chains, and a plain green face bandana pulled up for a hit.
        'CK': [piece('hat', 0.45, beanie), piece('neck', 0.8, chain, tops=open_collar), piece('mask', 1.0, mask_colour, when='attack')],
        # Hellfire MC: bikers. A biker skull cap (half helmet), black wraparounds, and a skull-print bandana for a hit.
        'IR': [piece('hat', 0.4, skull_cap), piece('glasses', 0.55, shades_biker), piece('mask', 1.0, mask_skull, when='attack')],
        # Vinewood Elite: money. A royal blue trilby on some of the men, a blue tie with every shirt, aviators.
        # No masks: they don't hide their faces.
        'VS': [piece('hat', 0.35, trilby), piece('neck', 1.0, tie, only='shirt'), piece('glasses', 0.5, shades_smart)],
        # Your crew, in your colour.
        'player_street': [piece('hat', 0.4, beanie), piece('neck', 0.8, chain, tops=open_collar)],
        'player_biker': [piece('hat', 0.35, skull_cap), piece('glasses', 0.5, shades_biker)],
        'player_smart': [piece('hat', 0.3, trilby), piece('neck', 1.0, tie, only='shirt'), piece('glasses', 0.4, shades_smart)],
        'player_mixed': [piece('hat', 0.3, beanie), piece('glasses', 0.3, shades_smart), piece('neck', 0.6, chain, tops=open_collar)],
        # Bosses and lieutenants: a step up, so you can pick them out in a fight.
        'lead': [piece('neck', 1.0, gold_chain, tops=open_collar), piece('watch', 1.0, watch), piece('glasses', 1.0, shades_smart)],
    }

    path = os.path.join(os.path.dirname(__file__), '..', '..', 'src', 'Content', 'outfits.json')
    data = json.load(io.open(path, encoding='utf-8'))
    data['extras'] = extras

    # Hair: a wide range of real cuts by their game names (first drawable with that name), and short cuts for anyone
    # under a hat or helmet, so long hair never pokes through.
    hair_m = load(folder, 'male_hair'); hair_f = load(folder, 'female_hair')
    def hair_ids(d, names):
        out = []
        for n in names:
            for k, tex in d.items():
                if any((v.get('Localized') or '').startswith(n) for v in tex.values()):
                    out.append(int(k)); break
        return sorted(set(out))
    data['hair'] = {
        'm': hair_ids(hair_m, ['Buzzcut', 'Faux Hawk', 'Hipster Shaved', 'Side Parting Spiked', 'Shorter Cut', 'Biker',
                               'Ponytail', 'Cornrows', 'Slicked', 'Short Brushed', 'Spikey', 'Caesar', 'Chopped', 'Dreads',
                               'Long Hair', 'Shaggy Curls', 'Surfer Dude', 'Short Side Part', 'High Slicked Sides',
                               'Long Slicked', 'Hipster Youth', 'Mullet', 'Classic Cornrows', 'Palm Cornrows', 'Hightop',
                               'Loose Swept Back', 'Undercut Swept Back', 'Undercut Swept Side', 'Mod', 'Flattop',
                               'Military Buzzcut', 'Afro Faded', 'Two Block', 'Short Curls Fade']),
        'f': hair_ids(hair_f, ['Short', 'Layered Bob', 'Pigtails', 'Ponytail', 'Braided Mohawk', 'Braids', 'Bob',
                               'Faux Hawk', 'French Twist', 'Long Bob', 'Loose Tied', 'Pixie', 'Shaved Bangs', 'Top Knot',
                               'Wavy Bob', 'Messy Bun', 'Tight Bun', 'Twisted Bob', 'Big Bangs', 'Pinched Cornrows',
                               'Leaf Cornrows', 'Wave Braids', 'Coil Braids', 'Rolled Quiff', 'Loose Swept Back',
                               'Undercut Swept Back', 'Neat Bun', 'Short Bob', 'Afro']),
    }
    # Uniforms: each crew wears one outfit, the same item in the same named colour for everyone, with the same
    # trousers and shoes; faces, hair, beards and the odd hat or shades make them individuals. Your crew wears the
    # uniform of the style you pick when it comes in your colour (looks marked "uniform"), the nearest look otherwise.
    UNIFORM = {'CK': {'m': 384, 'f': 407}, 'IR': {'m': 424, 'f': 452}, 'VS': {'m': 140, 'f': 137}}
    PLAYER_UNIFORM = {'player_street': 'CK', 'player_biker': 'IR', 'player_smart': 'VS'}
    def top_of(look):
        return next((p['d'] for p in look['parts'] if p['c'] == 11), None)
    styles = data['styles']
    for crew, tops in UNIFORM.items():
        kept, seen = [], set()
        for look in styles[crew]:
            if top_of(look) == tops[look['g']] and look['g'] not in seen:
                seen.add(look['g'])
                look['uniform'] = True
                look['legs'] = (look.get('legs') and crew.lower()) or look.get('legs')
                look['shoes'] = crew.lower()
                kept.append(look)
        assert len(kept) == 2, (crew, len(kept))
        styles[crew] = kept
    # Vinewood Elite: a black long suit jacket for everyone, the colour in the trousers (blue chinos for men, the blue
    # suit trousers for women), a shirt and a tie in the colour.
    tops_m = load(folder, 'male_tops'); tops_f = load(folder, 'female_tops')
    legs_m = load(folder, 'male_legs'); legs_f = load(folder, 'female_legs')
    for look in styles['VS']:
        g = look['g']
        jacket = by_name(tops_m if g == 'm' else tops_f, 'Black Long Suit')
        trousers = coloured(legs_m, 'Chinos') if g == 'm' else coloured(legs_f, 'Suit')
        assert jacket and trousers and 'blue' in trousers['t'], (g, jacket, trousers)
        parts = [p for p in look['parts'] if p['c'] not in (11, 4)]
        parts.insert(0, {'c': 11, 'd': jacket[0], 't0': jacket[1]})
        parts.append({'c': 4, 'd': trousers['d'], 't': trousers['t']})
        look['parts'] = parts
        look['legs'] = None   # the coloured trousers are part of the look
        if g == 'f': look['under'] = 'plain'  # the plain White Shirt: the White Blouse's collar flares out of this jacket
    for pstyle, crew in PLAYER_UNIFORM.items():
        for look in styles[pstyle]:
            look.pop('uniform', None)
        uni = [json.loads(json.dumps(l)) for l in styles[crew]]   # the same uniform, in your colour
        styles[pstyle] = uni + [l for l in styles[pstyle] if top_of(l) not in (UNIFORM[crew]['m'], UNIFORM[crew]['f'])]
    # one pair of trousers and shoes per crew (the first of the style's own neutral lists)
    base = {'CK': 'street', 'IR': 'biker', 'VS': 'suit'}
    base_shoes = {'CK': 'street', 'IR': 'boots', 'VS': 'formal'}
    for g in ('m', 'f'):
        n = data['neutral'][g]
        for crew, cat in base.items():
            n['legs'][crew.lower()] = n['legs'][cat][:1]
            n['shoes'][crew.lower()] = n['shoes'][base_shoes[crew]][:1]

    data['hatHair'] = {
        'm': hair_ids(hair_m, ['Buzzcut', 'Shorter Cut', 'Short Brushed', 'Caesar', 'Short Side Part', 'Military Buzzcut', 'Cornrows']),
        'f': hair_ids(hair_f, ['Short', 'Pixie', 'Tight Bun', 'Pinched Cornrows', 'Short Bob']),
    }
    io.open(path, 'w', encoding='utf-8', newline='').write(json.dumps(data, indent=1, ensure_ascii=False) + '\n')
    for style, pieces in extras.items():
        print(style, [(p['slot'], {g: [(x['d'], len(x.get('t', {})) or 'fixed') for x in p[g]] for g in ('m', 'f')}) for p in pieces])


if __name__ == '__main__':
    main()
