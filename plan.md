# Plan

Yerel not dosyası (`.git/info/exclude` ile commit dışı). Şu an: ilk minigame'in tasarımı.

## Genel kurallar

### Oyun yapısı
- Party oyunu. Her el bir minigame oynanır.
- Minigame sonucunda sıralamaya göre puan alınır.
- Oyun bitişi: belirli sayıda minigame oynanınca **ya da** belirli bir skora ulaşılınca.
- Bu ayarlar rastgele olabileceği gibi custom da ayarlanabilir:
  - İstenmeyen oyunlar hariç tutulabilir.
  - Bir oyun birden çok kez oynanabilir.
- Party akışı (sıra, puan, ayarlar) şimdilik kapsam dışı. Önce tek bir minigame.

### Temel mekanik
- Ateş eden oyuncu küp (küp olmak zorunda değil, boyut oranı 1:1:1'e yakın bir şey olması tercih edilir).
- Atışların etkisi minigame'e göre değişir:
  - Vurulanın canını azaltır.
  - Vurulana geri tepme (knockback) uygular.
  - Sadece patlama yaratır, patlamayla yükselmeye yarar.
- Hareket: WASD.
- Zıplama minigame'e göre açık/kapalı.

### Ağ
- Demo için LAN: bir oyuncu host, diğerleri client.
- İleride online servera geçilebilecek şekilde düşünülmeli.
- LAN oyunun tanımlayıcı özelliği değil, üzerinde durulmayacak.

### Kamera
- Genel olarak shooter olduğu için TPS kamera.
- Bazı minigame'lerde değişebilir.

### Çalışma kuralları
- Ortak oyun `Assets/ShooterParty/` altında (`Core/` = framework, `MiniGames/KnockbackArena/` = mini-game + ortak art). Herkes burada çalışır (2026-10-31'de Ezel'in klasöründen taşındı).
- `Assets/Abd/` Abd'nin deneme alanı (ilk Knockback Arena prototipi burada). `Ardil`, `Kha` klasörleri onların deneme alanı, dokunulmaz.
- Unity'nin kendiliğinden doldurduğu `.meta` değişiklikleri (özellikle Ezel klasöründekiler) commit edilmez. `.meta` dosyaları asla silinmez.

---

## Minigames

### Knockback Arena

**Tek cümle:** Düz bir platformda herkes birbirine mermi atar. Vurulan, merminin geldiği yönde savrulur ve her vuruşta hafifler, böylece bir sonraki vuruşta daha uzağa uçar. Platformdan düşen ölür.

#### Kurallar
- Zemin düz bir dikdörtgen prizma (plane gibi) platform. Üzerinde güvenli/korunaklı bir yer yok. Oyuncular mermiden kaçarak hayatta kalmaya çalışır.
- Ateş etmek bedava, sadece atış hızı sınırı var (mermi sayısı/cephane yok).
- Mermiler gerçek, fiziksel objeler (hitscan değil), sahneye spawn edilir.
- Mermi atan kişiye etki etmez.
- Mermi bir oyuncuya değince oyuncu, merminin geldiği yönden bir kuvvetle geri tepme (knockback) alır.
- Her vurulduğunda oyuncunun **kütlesi** azalır (hacmi/boyutu değişmez).
- Mermi her seferinde aynı kuvveti uygular. Kütle azaldıkça aynı kuvvet oyuncuyu daha çok savurur. Yani vuruldukça platformdan düşme ihtimali artar.
- Platformdan düşen oyuncu ölür.
- Denge ayarı (kuvvet, kütle azalması, atış hızı vb.) sonradan elle yapılır, o yüzden tüm değerler Inspector'dan değiştirilebilir olmalı.

#### Temel oyuncu (1. parça: mekanik)
- Oyuncu bir küp. Küpün "burnunda" bir ateş noktası (muzzle) var, mermi oradan çıkar.
- **Nişan/kamera:** TPS. Mouse ile nişan alınır. Mouse karakterin yönünü de çevirir.
- **Ateş:** Sol tık, baktığı yere ateş. Atış hızı sınırlı.
- **Hareket:** WASD, `AddForce` ile değil. Basılınca anında sabit hızla gider (ivmelenme yok).
- **Zıplama yok** (zıplamak çok güçlü, sonraki aşamada power-up olarak gelecek).

#### Sonraki aşamalar (şimdi YAPILMAYACAK)
- **2. parça: online.** Mekanik bitince.
- **Power-up'lar:**
  - Zıplama hakkı. Hafif kütleli oyuncu zıplayınca daha yukarı çıkar.
  - Shift ile dash. Süre aralıklı olabilir ya da power-up ile 3 dash hakkı gibi.

#### Prototip hedefi (bu aşamanın "bitti" tanımı)
- [x] Platform + küp oyuncu sahnede (`Assets/Abd/`).
- [x] WASD ile sabit hızlı hareket, mouse ile dönüş + TPS kamera. (8 m/s ölçüldü; mouse hissi elle denenecek)
- [x] Sol tıkla atış hızı sınırlı mermi. (mermi aralığı 6.25 m = 4/sn, mermi yatay, atana etki yok)
- [x] Mermi değince knockback.
- [x] Her vuruşta kütle azalması, hafifleyen oyuncunun daha çok savrulması. (aynı vuruşta 8.1 → 9.0 → 10.0 → 11.1 m)
- [x] Platformdan düşünce ölüm. (`ArenaMatch` kalan sayısını logluyor)
- [x] Test için hedef (dummy/bot/ikinci oyuncu), bkz. Soru 12.

---

## Karar bekleyen sorular (Knockback Arena)

Cevap verirken sadece numara + harf yazman yeterli (örn. `1A 2B 3C`). `(öneri)` benim tercihim. Her soruda "Farketmez" seçeneği var, onu seçersen ben öneriyi uygularım.

**1. Karakter fiziği nasıl olacak?** Kütle azalması fiziksel olduğu için bunu en başta seçmek gerekiyor.
- xA) Rigidbody. Hareket hızı kodla ayarlanır, knockback ve kütle Unity fiziğiyle çalışır. (öneri)
- B) CharacterController. Knockback ve kütle etkisi elle hesaplanır.
- C) Farketmez / önemli değil.

**2. Knockback alırken oyuncu hareket edebilecek mi?** WASD "anında sabit hız" olduğu için hareket, savrulmayı hemen silebilir. Bunun çözümü gerekli.
- x(aslında belki addforce ile hareket yaparız ama eğrisi çok hızlı çıkar bu syaede sanki ani hareket ediyormuş gibi olur. durunca hemen durur ya da yön değiştirince. ama bilemedim sence nasıl yaparlım? )A) Knockback süresince (kısa bir an) hareket kontrolü kilitlenir, süre bitince kontrol geri gelir. (öneri)
- B) Hareket kilitlenmez, ama savrulma hızı zamanla sönerken WASD hızıyla toplanır.
- C) Oyuncu havadayken (platformdan çıkınca) kontrol yok, zemindeyken serbest.
- D) Farketmez / önemli değil.

**3. Dikey nişan olacak mı?** Yani mouse yukarı/aşağı baktığında mermi de yukarı/aşağı mı gider?
- x(kamera istediği yöne baksın ama bu minigame özelinde yatay ateş edilsin bence sadece.)A) Sadece yatay nişan. Mermi hep zemine paralel gider, kamera dikey olarak sabit/hafif. (öneri, düz zeminde en sade)
- B) Hem yatay hem dikey nişan, mermi kameranın baktığı yöne gider.
- C) Farketmez / önemli değil.

**4. Knockback yönü neye göre olacak?**
- (mermiler yatay geleceği için yatay olsun )A) Sadece yatay düzlemde, merminin geliş yönünün yatay bileşeni. Oyuncu yukarı fırlamaz, kayarak savrulur. (öneri)
- B) Merminin tam 3B yönünde (yukarı/aşağı bileşeni de olur).
- C) Farketmez / önemli değil.

**5. Kütle azalması nasıl işleyecek?**
- x(çarpan daha makul gibi .zira negatif kütle gibi şeylerden de korur. alt sınır koymayabiliriz çok komik olur. alt sınır 0 olmaması olabilir. şuan için alt sınır olmasın)A) Her vuruşta yüzdelik çarpan (örn. kütle × 0.9). Hızlıca sıfıra inmez, azalma yavaşlar. Alt sınırı ayrıca koyarız. (öneri)
- B) Her vuruşta sabit miktar çıkar (örn. -0.5). Belli vuruştan sonra çok hafifler.
- C) Farketmez / önemli değil.

**6. Mermi davranışı ne olsun?**
- xA) Düz, sabit hızla gider, yerçekimi yok. Bir şeye çarpınca ya da belli süre sonra yok olur. (öneri)
- B) Yerçekimli, eğri uçan mermi.
- C) Farketmez / önemli değil.

**7. Mermi nereye gitsin?** (kamera arkada, karakter öndeyken fark oluşabilir)
- x (klamera ile atış yönü fixlenmiş)A) Ekran ortasındaki (crosshair) noktaya doğru, burundaki ateş noktasından çıkar. (öneri, "baktığı yere atış" hissi)
- B) Karakterin burun yönüne düz, kameradan bağımsız.
- C) Farketmez / önemli değil.

**8. Başlangıç atış hızı ne kadar olsun?** (Inspector'dan değişecek, sadece başlangıç değeri)
- A) Saniyede yaklaşık 2 mermi (yavaş, her atış önemli).
- x (unityden dengeleme yaparken ayarlanabilir olsun. sonra bakarız)B) Saniyede yaklaşık 4 mermi. (öneri)
- C) Saniyede yaklaşık 8 mermi (yoğun ateş).
- D) Farketmez / önemli değil.

**9. Oyuncular birbirine fiziksel olarak çarpabilir mi?** (küp küpü itebilir mi)
- x (birbirini itebilsin evet.)A) Evet, normal fizik çarpışması. Oyuncular birbirini itebilir. (öneri, Rigidbody ile doğal)
- B) Hayır, oyuncular birbirinin içinden geçer, sadece mermi etki eder.
- C) Farketmez / önemli değil.

**10. Ölünce ne olacak?** (party sıralaması için önemli)
- xA) Düşen elenir, izleyici olur. Son kalan kazanır, sıralama ölüm sırasına göre. (öneri)
- B) Düşen belli süre sonra tekrar doğar, el süre dolunca biter (en çok hayatta kalan/en az ölen kazanır).
- C) Farketmez / önemli değil.

**11. Kütlenin azaldığını oyuncu nasıl görecek?** Boyut değişmeyecek, bir geri bildirim gerekebilir.
- xA) Küpün rengi/parlaklığı değişir (örn. soluklaşır).
- B) Ekranda küçük bir kütle göstergesi (UI).
- C) Şimdilik hiçbir şey, sonra düşünürüz. (öneri, önce mekanik)
- D) Farketmez / önemli değil.

**12. Tek başına test için karşımızda ne olacak?** Online yokken knockback'i denemek için hedef gerekli.
- xA) Hareketsiz dummy küpler (vurunca knockback ve kütle azalması çalışır). (öneri)
- B) Rastgele hareket edip ateş eden basit bot.
- C) Aynı bilgisayarda ikinci oyuncu (ayrı klavye/gamepad).
- D) Farketmez / önemli değil.

**13. Karakter ve mermi görseli ne olsun?** (Ezel'in `CubeCharacter.fbx` ve `Shot.fbx` modelleri var)
- x (başka kimsenin modellere sakın elleme şuan, karakter küp, ateş ettiği ağzı silindir, attığı mermi küre olsun )A) Önce düz Unity primitive'leri (Cube/Sphere), modeller sonra takılır. (öneri)
- B) Baştan Ezel'in modellerini kullan (referansla, kopyalamadan).
- C) Farketmez / önemli değil.

**14. Kamera nasıl yapılsın?** (Projede Cinemachine paketi yok)
- x(basit olsun şuan için sonra cinemachine kısmında konuşuruz)A) Basit kendi kamera script'imiz, ek paket yok. (öneri)
- B) Cinemachine paketini ekleyip onunla kuralım.
- C) Farketmez / önemli değil.

---

## Kararlar (cevaplardan özet)

| # | Konu | Karar |
|---|------|-------|
| 1 | Fizik | Rigidbody. Hareket hızı kodla, knockback ve kütle Unity fiziğiyle. |
| 2 | Knockback sırasında hareket | Savrulma sırasında kısa bir kontrol kilidi. Normal hareket "hızlı ivmelenen sabit hız" (aşağıdaki not). |
| 3 | Nişan | Kamera istediği yöne bakabilir, ama bu minigame'de ateş **sadece yatay**. |
| 4 | Knockback yönü | Sadece yatay (mermiler yatay geldiği için). |
| 5 | Kütle azalması | Yüzdelik çarpan (örn. ×0.9). **Alt sınır yok**, çarpan olduğu için kütle hiç sıfır/negatif olmaz. |
| 6 | Mermi | Düz, sabit hız, yerçekimi yok. Çarpınca ya da süre sonunda yok olur. |
| 7 | Mermi hedefi | Ekran ortası (crosshair) hedefine doğru, burun ucundaki ateş noktasından çıkar. |
| 8 | Atış hızı | Başlangıç ~4/sn, ama dengeleme için Inspector'dan ayarlanabilir. |
| 9 | Oyuncu çarpışması | Oyuncular birbirini fiziksel olarak itebilir. |
| 10 | Ölüm | Düşen elenir ve izleyici olur. Son kalan kazanır, sıralama ölüm sırasına göre. |
| 11 | Kütle geri bildirimi | Küpün rengi/parlaklığı kütleye göre değişir (soluklaşır). |
| 12 | Test hedefi | Hareketsiz dummy küpler. |
| 13 | Görsel | Düz primitive'ler: karakter **küp**, ateş ağzı **silindir**, mermi **küre**. Ezel'in modellerine (ve başka kimsenin dosyasına) dokunulmayacak. |
| 14 | Kamera | Basit kendi script'imiz. Cinemachine'i sonra konuşuruz. |

**Soru 2 notu (hareket hissi):** `AddForce` ile hareket etmek yerine hızı doğrudan sürüyoruz, ama anında değil: hedef hıza çok hızlı bir ivmeyle gidiyoruz (`Vector3.MoveToward` benzeri, yüksek ivme/yavaşlama değeri). Böylece hareket hâlâ "sabit hız, anında basıp bırakma" hissi verir ama ivme değerini ayarlayabiliriz. Dezavantajı: tuşa basmayınca hedef hız 0 olduğu için sistem savrulmayı da hemen sıfırlar. Bu yüzden vurulunca kısa bir **kontrol kilidi** (örn. 0.25 sn, ayarlanabilir) var, o sırada WASD hızı uygulanmaz ve savrulma `linearDamping` ile söner. Kilit bitince normal hareket döner.

## Yapım adımları (1. parça: mekanik)

Hepsi `Assets/Abd/` altında. Örnek yapı: `Assets/Abd/KnockbackArena/{Scenes,Scripts,Prefabs,Materials}`.

1. **Klasörler ve sahne.** `Scenes/KnockbackArena.unity` (Playground sahnesine dokunmadan, yeni sahne).
2. **Platform.** Düz dikdörtgen prizma (basık Cube), düşük sürtünmeli `PhysicsMaterial`, etrafında korkuluk yok. Altında bir ölüm bölgesi (kill zone, belli bir Y'nin altı).
3. **Ayarlar (`ScriptableObject`).** Dengeleme tek yerden yapılsın: hareket hızı, ivme, atış hızı, mermi hızı/ömrü, knockback impulse'u, kütle çarpanı, kontrol kilidi süresi, `linearDamping`.
4. **Oyuncu prefab'ı.** Küp + Rigidbody (X/Z dönüşü donuk, yaw'ı kodla vereceğiz), burnunda silindir "ateş ağzı" child'ı ve mermi çıkış noktası (muzzle) boş objesi, her oyuncuya ayrı materyal (renk değişimi için).
5. **Girdi.** Yeni Input System (projede 1.18.0 var). Ortak `Assets/InputSystem_Actions.inputactions` dosyasını değiştirmeyeceğiz, kendi `InputAction`'larımızı (WASD, mouse delta, sol tık) kod ya da `Abd` içinde ayrı bir asset olarak tanımlarız. İmleç kilitlenir.
6. **Hareket.** WASD karakterin baktığı yöne göre, hızlı ivmeli sabit hız. Mouse yatay hareketi karakteri döndürür.
7. **TPS kamera.** Karakterin arkasında/üstünde, mouse dikey hareketi kameranın eğimini değiştirir. Çarpışma/duvar kontrolü yok. Crosshair ekran ortasında.
8. **Ateş.** Sol tık, atış hızı sınırı (cooldown). Ateş yönü: crosshair hedefinin **yatay** bileşeni (dikey sıfırlanır), mermi muzzle'dan çıkar.
9. **Mermi.** Kürede Rigidbody, yerçekimi yok, sabit hız, ömür sonunda yok olur. Atan oyuncuyla çarpışmayı yok sayar (atana etki yok). Bir şeye çarpınca yok olur.
10. **Vurulma (alıcı bileşen).** Mermi bir oyuncuya değince: yatay mermi yönünde sabit bir impulse uygular (`ForceMode.Impulse`, ivme = impulse / kütle), sonra `rb.mass *= çarpan`, kontrol kilidini başlatır, rengi günceller.
11. **Ölüm.** Kill zone'a düşen oyuncu elenir: kontrol kapanır, olay (event) fırlatılır (`OnEliminated`), ölüm sırası kaydedilir (party tarafı için), izleyici durumuna geçer (şimdilik sadece kontrolsüz kamera).
12. **Dummy hedefler.** Hareketsiz küpler, aynı alıcı bileşen ve Rigidbody, girdi/ateş yok. Yerleştirip knockback ve kütle azalmasını test etmek için.
13. **Test ve ayar.** Kontrol listesi: ateş hızı sınırı çalışıyor mu, atana etki yok mu, her vuruşta daha uzağa savruluyor mu, düşünce ölüm tetikleniyor mu, iki küp birbirini itebiliyor mu. Sonra değerler elle dengelenir.

**Online'a hazırlık notu (şimdi yapılmayacak ama kod buna göre yazılacak):** Girdiyi okuyan kod ile hareketi/ateşi uygulayan kod ayrı tutulur. Böylece online aşamasında girdi ağdan gelebilir ve fizik sadece host'ta çalıştırılabilir.

## Uygulama notları (kodlama sırasında alınan kararlar)
- Kod: `Assets/Abd/KnockbackArena/Scripts/` (namespace `Abd.KnockbackArena`). Sahne/prefab/materyaller `Abd > Knockback Arena > Build Scene` menüsüyle kurulur (`Scripts/Editor/KnockbackArenaBuilder.cs`).
- Mermi fizik motoruna karışmaz, her karede SphereCast ile tarar (tünelleme yok, hedefi kendisi itmez). Knockback `VelocityChange` ile (hız değişimi = impulse / mevcut kütle), sonra kütle çarpanı uygulanır.
- Ateş yönü şimdilik kameranın yaw'ı (yatay). Kamera karakterin tam arkasında olduğu için crosshair yönüyle aynı. Omuz kamerası eklenirse aim-point hesabı gerekir.
- `airControl` ayarı eklendi (varsayılan kapalı): havadayken (platformdan çıkınca) WASD etkisiz, böylece düşen oyuncu havada geri dönemez.
- Knockback vurulma sırası: önce mevcut kütleyle savurur, sonra kütle azalır.
- Ölüm: `killY` altına düşen elenir, `ArenaMatch.EliminationOrder` sıralamayı tutar.

## Açık maddeler
- [x] 1. parça (mekanik) kodlandı ve Play mode'da test edildi (2026-10-28, unity-cli ile). Mouse/klavye hissi elle denenmedi.
- [ ] Kontrol kilidi süresi, impulse, kütle çarpanı gibi sayısal değerler oyun içinde dengelenecek.
- [x] ~~2. parça (online) mekanik bitince planlanacak.~~ İptal (2026-10-28): online sistemi başka bir arkadaş yaptı, oradan alınacak. `abd/knockback` branch'i online'sız olduğu gibi kalır, ayrıca main'e de merge edilir (sahneden görüntü almak ya da main'deki işe buradan feature taşımak için).
- [ ] İlk kural listesinde boş bırakılan iki madde (Genel kurallar) istenirse sonradan doldurulacak.
