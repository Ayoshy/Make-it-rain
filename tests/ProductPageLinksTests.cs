using Battlestation.Shopping;

internal static class ProductPageLinksTests
{
    // Reduced observed markup from the three local 2026-09-22 fridge snapshots.
    // Navigation/action rejection and the size bound below use synthetic markup.
    public static void Run(Action<bool,string> check)
    {
        var spec=ShoppingSpecParser.Deterministic("réfrigérateur no frost 300 L");
        const string boulangerPage="https://www.boulanger.com/c/refrigerateur-avec-congelateur";
        const string boulangerProduct="https://www.boulanger.com/ref/9000998132";
        const string boulanger="""
            <a class="navigation__link" href="/c/refrigerateur" role="menuitem"><span class="navigation__name">Réfrigérateur</span></a>
            <a href="/ref/9000998132" class="product-list__product-image-link analytic-track-origin" data-tracking-action="clicProduit" data-analytics_product_name="refrigerateur_combine_chiq_fbm157l42" data-analytics_product_unitprice_ati="259.99">
              <picture><img src="https://boulanger.scene7.com/is/image/Boulanger/8592344700774_h_f_l_0" alt="" class="product-list__product-image" aria-hidden="true"/></picture>
            </a>
            <a href="/ref/9000998132" class="analytic-track-origin" data-tracking-action="clicProduit">
              <span class="product-list__product-brand">CHIQ</span>
              <h2 id="productLabel" class="product-list__product-label">Réfrigérateur combiné CHIQ FBM157L42</h2>
            </a>
            """;
        var found=ProductPageLinks.Read(boulanger,boulangerPage,spec);
        check(found.Count==1&&found[0].Url==boulangerProduct,"La carte Boulanger conserve le href réel /ref et dédoublonne image et titre");
        check(found[0].Title.Contains("FBM157L42",StringComparison.OrdinalIgnoreCase),"La référence observée Boulanger survit à une première ancre image sans alt");

        const string electroPage="https://www.electrodepot.fr/gros-electromenager/refrigerateur/refrigerateur-multi-portes.html";
        const string electroProduct="https://www.electrodepot.fr/refrigerateur-4-portes-valberg-4d-474-e-b625c.html";
        const string electro="""
            <a class="productlist-item" href="https://www.electrodepot.fr/refrigerateur-4-portes-valberg-4d-474-e-b625c.html" :href="product.item.itemUrl" v-if="product.item.itemType === 'PRODUCT'" @click="onClick(product)">
              <div class="productlist-item_box"><figure class="productlist-item--img">
                <img src="https://www.electrodepot.fr/media/catalog/product/cache/50e64802d8187e9f9ddbcc3156725056/P993820.jpg?frz-v=4764" :src="product.item.thumbnailUrl" title="Réfrigérateur 4 portes VALBERG 4D 474 E B625C" :title="product.item.name" alt="Réfrigérateur 4 portes VALBERG 4D 474 E B625C" :alt="product.item.name"/>
              </figure><div class="productlist-item_content"><h2 class="productlist-item--name" v-html="product.item.name">Réfrigérateur 4 portes VALBERG 4D 474 E B625C</h2>
                <ul><li>Capacité : 474 L</li><li>Type de froid : No frost</li></ul>
              </div></div>
            </a>
            """;
        found=ProductPageLinks.Read(electro,electroPage,spec);
        check(found.Count==1&&found[0].Url==electroProduct,"La carte Electro Dépôt suit son href rendu, pas l'expression Vue :href");
        check(found[0].Title.Contains("VALBERG 4D 474 E B625C"),"Le modèle réellement affiché dans la carte Electro Dépôt est conservé");
        const string dynamicOnly="""
            <script type="application/json">{"displayInfo":{"itemUrl":"https://www.electrodepot.fr/refrigerateur-invente.html","name":"Réfrigérateur TEST R123"}}</script>
            <a class="productlist-item" :href="product.item.itemUrl"><h2>Réfrigérateur TEST R123</h2></a>
            """;
        check(ProductPageLinks.Read(dynamicOnly,electroPage,spec).Count==0,"Une URL d'hydratation ou une expression :href seule ne devient pas une ancre réelle");

        const string excedentPage="https://excedent-electromenager.fr/49-refrigerateur-multiportes";
        const string excedentProduct="https://excedent-electromenager.fr/refrigerateur-multiportes/111254-.html";
        const string excedent="""
            <a class="dropdown-item" href="https://excedent-electromenager.fr/49-refrigerateur-multiportes" data-depth="2">Réfrigérateur multiportes</a>
            <a href="https://excedent-electromenager.fr/refrigerateur-multiportes/111254-.html" class="thumbnail product-thumbnail">
              <img itemprop="image" src="https://excedent-electromenager.fr/25586-home_default/product.jpg" alt="REFRIGERATEUR MULTIPORTES NO FROST BLACK" data-full-size-image-url="https://excedent-electromenager.fr/25586-large_default/default.jpg"/>
            </a>
            """;
        found=ProductPageLinks.Read(excedent,excedentPage,spec);
        check(found.Count==1&&found[0].Url==excedentProduct&&found[0].Title=="REFRIGERATEUR MULTIPORTES NO FROST BLACK",
            "La carte Excedent utilise son véritable alt sans inventer une référence ou une URL depuis le slug incomplet");

        const string controls="""
            <nav><a href="/ref/123456" class="product-link">Réfrigérateur TEST MENU123</a></nav>
            <aside><a href="/c/refrigerateur/brand~haier">Réfrigérateur HAIER</a></aside>
            <a href="/c/refrigerateur" class="navigation__link">Réfrigérateur</a>
            <a href="/login" class="product-link">Réfrigérateur TEST LOGIN123</a>
            <a href="/cart/add/123" class="product-link">Réfrigérateur TEST CART123</a>
            <a href="/images/refrigerateur-123.jpg" class="product-link">Réfrigérateur TEST IMAGE123</a>
            <a href="javascript:openProduct(123)" class="product-link">Réfrigérateur TEST JS123</a>
            <a href="mailto:shop@example.com" class="product-link">Réfrigérateur TEST MAIL123</a>
            <a href="https://other-shop.example/product/123" class="product-link">Réfrigérateur TEST FOREIGN123</a>
            <a href="http://127.0.0.1/product/123" class="product-link">Réfrigérateur TEST LOCAL123</a>
            <script>const fake = '<a href="/ref/999999" class="product-link">Réfrigérateur TEST SCRIPT123</a>';</script>
            <a href="../ref/9000998132" class="product-link">Réfrigérateur combiné CHIQ FBM157L42</a>
            <a href="/ref/9000998132" class="product-link">Réfrigérateur combiné CHIQ FBM157L42</a>
            """;
        found=ProductPageLinks.Read(controls,boulangerPage,spec);
        check(found.Count==1&&found[0].Url==boulangerProduct,"Menus, catégories, actions, images, scripts et destinations étrangères ou privées sont exclus");
        check(ProductPageLinks.Read("<a class='product-link' href='/ref/123'>Réfrigérateur TEST R123</a>","http://192.168.1.8/catalog",spec).Count==0,
            "Une page de base privée ne rend pas ses liens relatifs admissibles");

        const string embeddedMarkup="""
            <script>const small = value < 10; const fragment = "<iframe src='placeholder'>";</script>
            <div data-config='{"icon":"<svg version=\"1.2\" xmlns=\"http://www.w3.org/2000/svg\">"}'>
              <svg/>
              <a class="product-link" href="/ref/9000998132">Réfrigérateur combiné CHIQ FBM157L42</a>
            </div>
            <svg><path d="M0 0"/></svg>
            """;
        found=ProductPageLinks.Read(embeddedMarkup,boulangerPage,spec);
        check(found.Count==1&&found[0].Url==boulangerProduct,
            "Le HTML cité dans JavaScript ou un attribut JSON ne masque pas les vraies ancres produit suivantes");

        var many=string.Join("",Enumerable.Range(0,32).Select(index=>$"<a class='product-link' href='/ref/{9001000000L+index}'>Réfrigérateur TEST R{1000+index}</a>"));
        found=ProductPageLinks.Read(many,boulangerPage,spec);
        check(found.Count==24&&found.Select(item=>item.Url).Distinct().Count()==24,"La découverte locale borne les fiches à vingt-quatre liens distincts");
        check(found.All(item=>Enumerable.Range(0,32).Any(index=>item.Url==$"https://www.boulanger.com/ref/{9001000000L+index}")),
            "La découverte locale ne produit que des URLs présentes dans les ancres fournies");
    }
}
