# Rai - Package tile manager

Converte un tile package di ArcGIS Pro (`.tpkx` o `.tpk`) in una cartella di tile `{z}/{x}/{y}` e la serve a GEOlayers 3 in After Effects, da questo PC o da un web server. Sostituisce lo script Python e `python -m http.server`: si usa senza riga di comando e, prima di convertire, controlla che le tile si allineino alla mappa di GEOlayers.

![La scheda Importa con un pacchetto pronto da convertire](docs/screenshot.png)

Le immagini usano dati di esempio sintetici.

## Scaricare e avviare

Scarica `rai-package-tile-manager.zip` dalla pagina [Releases](https://github.com/bitoesposito/rai-tile-package-manager/releases/latest), estrai i file e apri `Rai - Package tile manager.exe`. Non serve installarla: usa .NET Framework 4.8, già presente in Windows 10 e 11.

L'exe non è firmato, quindi al primo avvio SmartScreen può mostrare "PC protetto da Windows": fai clic su "Ulteriori informazioni" e poi su "Esegui comunque". Puoi anche trascinare un pacchetto, ed eventualmente la cartella di un progetto, sull'icona dell'exe.

## Come si usa

Le tre schede sono nella banda blu in alto, accanto ai pulsanti della finestra.

### Importa

1. Trascina il pacchetto sul riquadro scuro, o fai clic per sceglierlo. L'app legge lo schema di tassellatura e dice subito se funziona in GEOlayers.
2. In "Dove salvare le tile" scegli una delle due opzioni.
   - Nuovo progetto: l'app crea una cartella con il nome del pacchetto nella posizione indicata. All'inizio è la cartella del pacchetto; la cambi con "Cambia posizione…". Se scegli il Desktop, le tile vanno in `Desktop\nome_del_pacchetto` e mai sparse sul Desktop; se il nome è già usato, la cartella diventa `nome_del_pacchetto (2)`.
   - Aggiungi a un progetto esistente: scegli la cartella di un progetto già creato (vedi [più sotto](#aggiungere-tile-a-un-progetto-esistente)).
3. Premi "Converti". Alla fine lo stesso pulsante diventa "Metti in onda" e porta alla scheda In onda con il server già avviato.

### In onda

![La scheda In onda con il server attivo](docs/in-onda.png)

Qui scegli il progetto e la porta (8000 di default). Dopo una conversione l'app propone il progetto appena creato e, alla riapertura, l'ultimo usato. "Metti in onda" avvia il server locale: il riquadro rosso IN ONDA conferma che GEOlayers può caricare le tile e conta le richieste, e un pallino rosso accanto alla scheda lo ricorda dalle altre schede.

Sotto ci sono una tile del progetto e l'URL per GEOlayers; un clic sul riquadro lo copia negli appunti:

```
http://localhost:8000/{z}/{x}/{y}.png
```

Se converti un altro pacchetto mentre un progetto è in onda, "Metti in onda" passa al nuovo sulla stessa porta, quindi l'URL in GEOlayers non cambia.

Il server risponde solo a questo PC (127.0.0.1 e ::1), quindi non servono permessi di amministratore e Windows non chiede di aprire il firewall. Resta attivo finché la finestra è aperta. Anche una cartella di rete (`\\server\condivisione\...`) si può scegliere come progetto e servire così.

### Anteprima mappa

![L'anteprima della mappa con il riquadro della comp Full HD](docs/anteprima.png)

"Anteprima mappa", nella scheda In onda, apre il progetto in una finestra a parte. Legge le tile dalla cartella, quindi funziona anche a server fermo. Trascina per spostarti e cambia zoom con la rotella, i tasti + e − o un doppio clic; "Adatta all'area" torna all'extent del progetto. Dove a quello zoom non ci sono tile, la mappa è tratteggiata.

Il riquadro al centro è l'area che entra in una comp Full HD quando GEOlayers mostra le tile a dimensione reale. L'etichetta sopra dice lo zoom che serve, quello per una comp 4K e la larghezza dell'area. Uno zoom che il progetto non ha è segnato "manca"; se è quello per il Full HD, anche il riquadro diventa arancione. In quel caso esporta un pacchetto con zoom più dettagliati e aggiungilo al progetto.

### Storage remoto

Se le tile del progetto sono su un web server, inserisci l'indirizzo della cartella del progetto (per esempio `https://tiles.azienda.it/mappa`) e premi "Verifica connessione". L'app scarica una tile del progetto scelto in In onda e la confronta con quella su disco. Se il server risponde, compare l'URL da copiare per GEOlayers e l'indirizzo resta salvato nel progetto.

## Usare le tile in GEOlayers 3

In GEOlayers 3 crea una Raster Source di tipo xyz. La scheda In onda mostra i valori già convertiti: un clic su un valore lo copia per il campo con lo stesso nome.

- URI: l'URL del server, per esempio `http://localhost:8000/{z}/{x}/{y}.png`, valido mentre il progetto è in onda.
- Min Zoom e Max Zoom: il primo e l'ultimo zoom del progetto.
- Tile Size: 256 px, come le tile del progetto. Con 512 px GEOlayers le ingrandisce al doppio e perdono nitidezza.
- Bounds: l'extent di ArcGIS Pro convertita da Web Mercator in gradi, nell'ordine ovest, sud, est, nord; con più pacchetti li comprende tutti.

L'app registra i bounds quando un pacchetto entra nel progetto. Se mancano, per esempio in un progetto del vecchio script, aggiungi di nuovo i pacchetti da Importa: le tile restano come sono e l'app calcola i bounds.

## Aggiungere tile a un progetto esistente

Serve quando esporti un secondo pacchetto della stessa mappa, per esempio una zona più piccola con zoom più dettagliati, e vuoi tenere un solo URL in GEOlayers. Nello schema Web Mercator le coordinate `{z}/{x}/{y}` valgono per tutto il mondo, quindi pacchetti con estensioni diverse si incastrano da soli. L'app controlla tre cose:

- lo schema di tassellatura, che per ogni pacchetto, anche il primo, deve essere quello di ArcGIS Online, Bing Maps e Google Maps;
- il formato delle immagini, che deve restare lo stesso (PNG con PNG, JPEG con JPEG) perché GEOlayers usa un URL con una sola estensione;
- gli zoom in comune. Un pacchetto di un'area piccola contiene anche gli zoom bassi, quasi vuoti fuori dalla sua area: se sostituisse le tile del progetto, a quegli zoom sparirebbe il resto della mappa. Per questo di default l'app tiene le tile già presenti e aggiunge solo quelle che mancano; "Sostituisci le tile in comune" serve quando hai riesportato la stessa mappa per aggiornarla.

Prima di convertire, l'app mostra quali zoom aggiunge il pacchetto, quali ci sono già e se lo stesso file è già stato importato.

## Cosa c'è nella cartella di un progetto

```
mappa/
├── metadata.json
├── 0/
│   └── 0/
│       └── 0.png
├── 1/
│   ├── 0/
│   │   ├── 0.png
│   │   └── 1.png
│   └── 1/
...
```

`metadata.json` registra il formato delle tile, l'indirizzo remoto verificato e lo storico dei pacchetti importati (file, nome, zoom, extent in gradi, data). Sono progetti anche le cartelle del vecchio script Python, che contengono solo le cartelle degli zoom; il Desktop o Documenti non lo sono mai.

## Problemi comuni

### Il pacchetto viene rifiutato per il sistema di riferimento, l'origine o la scala

GEOlayers lavora in Web Mercator. In ArcGIS Pro, nello strumento "Crea pacchetto tile mappa", scegli lo schema di tassellatura "ArcGIS Online / Bing Maps / Google Maps" e riesporta il pacchetto.

### GEOlayers non mostra nulla

Nella scheda In onda il riquadro deve dire IN ONDA. Se il contatore delle richieste resta a zero, l'URL in GEOlayers non è quello copiato dall'app: controlla porta ed estensione. Se salgono le tile non trovate, GEOlayers chiede zoom o zone che il progetto non ha: l'anteprima mappa mostra quali.

### La porta 8000 è occupata

La usa un altro programma, per esempio un `python -m http.server` rimasto aperto, oppure Windows l'ha riservata. Scegli un'altra porta e aggiorna l'URL in GEOlayers.

### La cartella di rete non è raggiungibile

L'app controlla la cartella quando la scegli e di nuovo prima di scrivere. Verifica la connessione al NAS o all'unità di rete e i permessi di scrittura.

### Pacchetti di ArcMap o di quote

I pacchetti Compact Cache V1 di ArcMap e quelli di quote in formato LERC non sono supportati. Riesporta con ArcGIS Pro in PNG o in JPEG.

## Sviluppo

Il codice è C# per WinForms su .NET Framework 4.8 e si compila con il .NET SDK 10, su Windows, Linux o WSL:

```bash
dotnet build src -c Release
```

L'exe compilato è `src/bin/Release/net48/Rai - Package tile manager.exe`.

Il self-check crea pacchetti sintetici e verifica la lettura dei bundle, le regole delle cartelle, l'aggiunta a un progetto, il server e i calcoli dell'anteprima. Gira anche su Linux:

```bash
dotnet run --project tests
```

| File | Contenuto |
|---|---|
| `src/TilePackage.cs` | Lettura del pacchetto, controllo dello schema e conversione dei bundle |
| `src/TileFolder.cs` | Cartelle dei progetti: riconoscimento, cartella dedicata, `metadata.json` |
| `src/TileServer.cs` | Server locale e verifica dello storage remoto |
| `src/Ui.cs` | Design system: colori, font, sottopancia, monitor, schede, pulsanti, logo, barra del titolo, riquadro dell'URL |
| `src/MainForm.cs` | La finestra con le tre schede |
| `src/MapPreview.cs` | Anteprima della mappa: tile del progetto, tratteggio dove mancano, riquadro della comp |
| `src/FolderPicker.cs` | Selettore di cartelle di Esplora file |
| `src/icon.svg` | L'icona dell'app; `src/app.ico` ne contiene le versioni da 16 a 256 px |

A ogni push GitHub Actions esegue il self-check e compila l'exe, scaricabile dagli artifact della run. Il push di un tag `vX.Y.Z` pubblica una release con uno zip che contiene l'exe e la licenza del font; un tag spostato e ripubblicato con `-f` sostituisce lo zip e lascia la descrizione. `PRODUCT.md` e `DESIGN.md` descrivono il prodotto e il design system.

## Dettagli tecnici

Un tile package è uno zip con i bundle Compact Cache V2 e i metadati (`root.json` nei `.tpkx`, `conf.xml` nei `.tpk`). Ogni bundle `L{livello}/R{riga}C{colonna}.bundle` contiene un blocco di 128×128 tile: un'intestazione di 64 byte, poi un indice di record da 8 byte con l'offset della tile nei 40 bit bassi e la dimensione nei 24 alti. Il livello diventa `z`, la colonna `x` e la riga `y`. Riga e colonna sono in esadecimale e oltre lo zoom 16 usano più di quattro cifre: il vecchio script ne leggeva quattro e perdeva quei livelli.

L'app legge i bundle in streaming, senza caricarli in memoria, fino a otto in parallelo. Formato ed estensione delle tile vengono dai metadati del pacchetto.

## Copyright

Copyright © Esri Italia.

Il font Inter Tight, incorporato nell'exe, ha licenza SIL Open Font License 1.1 (`src/fonts/OFL.txt`). Il logo Rai nell'intestazione è il marchio pubblicato su rai.it e i colori dell'interfaccia vengono da rai.it e rainews.it; Rai e il logo Rai sono marchi registrati di RAI Radiotelevisione italiana S.p.A.
