
# PrIMA (Proactive Inspection Monitoring App)
-Detect Early, Resolve Systematically-
## Product Description

**Document Type:** Product Definition  
**Product Category:** System Monitoring & Issue Management  
**Target Environment:** Hospital Information System (SIMRS)  
**Primary Users:** Product Owner, System Analyst, EDP / IT Support

---

# 1. Product Overview

## 1.1. Background

Dalam operasional Hospital Information System (SIMRS), terdapat berbagai proses bisnis yang melibatkan pembentukan data secara otomatis, integrasi antar-modul, sinkronisasi dengan sistem eksternal, serta pemrosesan transaksi yang saling berkaitan.

Tidak semua permasalahan yang terjadi pada proses tersebut dapat diketahui secara langsung ketika proses berlangsung.

Sebagian permasalahan baru teridentifikasi ketika pengguna menjalankan laporan tertentu, dilakukan rekonsiliasi data, atau ketika petugas melakukan pemeriksaan pada akhir periode operasional.

Kondisi tersebut menyebabkan beberapa permasalahan tidak segera diketahui, terakumulasi dalam jumlah besar, dan membutuhkan waktu serta upaya lebih besar untuk proses investigasi dan perbaikannya.

Diperlukan mekanisme pemantauan proaktif yang mampu mengidentifikasi kondisi tidak normal sedini mungkin, tanpa harus menunggu laporan pengguna atau pemeriksaan manual.

## 1.2. Product Description

System Exception Monitoring adalah aplikasi yang dirancang untuk melakukan pemantauan proaktif terhadap kualitas, konsistensi, dan integritas operasional sistem informasi rumah sakit.

Sistem menggunakan komponen independen yang disebut **Detector** untuk melakukan pemeriksaan terhadap kondisi tertentu pada berbagai modul SIMRS.

Setiap Detector bertanggung jawab mendeteksi satu jenis permasalahan berdasarkan aturan pemeriksaan yang telah ditentukan. Pemeriksaan dapat dijalankan secara otomatis, berkala, berdasarkan pemicu proses tertentu, maupun secara manual.

Apabila ditemukan kondisi yang tidak sesuai dengan kondisi yang diharapkan, Detector akan mengirimkan laporan kepada System Exception Monitoring melalui mekanisme komunikasi yang telah distandarkan.

Seluruh temuan dikelola secara terpusat melalui Dashboard yang digunakan oleh petugas EDP untuk melakukan investigasi, penanganan, pencatatan tindakan perbaikan, dan verifikasi penyelesaian.

Sistem ini juga menyediakan riwayat temuan dan penanganan sebagai dasar evaluasi kualitas serta keandalan sistem informasi.

---

# 2. Product Objectives

Tujuan utama pengembangan System Exception Monitoring adalah mengurangi keterlambatan identifikasi permasalahan dalam operasional SIMRS.

Secara spesifik, sistem bertujuan untuk:

1. **Early Problem Detection**  
   Mengidentifikasi kondisi tidak normal sedini mungkin sebelum permasalahan terakumulasi atau menimbulkan dampak yang lebih luas.

2. **Proactive Monitoring**  
   Melakukan pemeriksaan secara otomatis tanpa bergantung pada laporan atau inisiatif pengguna.

3. **System Quality Assurance**  
   Memantau konsistensi data, integritas transaksi, dan kesesuaian hasil pemrosesan sistem terhadap aturan bisnis yang diharapkan.

4. **Centralized Issue Management**  
   Menyediakan satu tempat untuk mengelola seluruh permasalahan yang ditemukan oleh berbagai Detector.

5. **Traceable Resolution**  
   Memastikan setiap permasalahan memiliki riwayat investigasi, tindakan perbaikan, dan penyelesaian yang dapat ditelusuri.

6. **Continuous Improvement**  
   Menyediakan informasi historis mengenai permasalahan yang terjadi sebagai dasar evaluasi dan peningkatan kualitas produk SIMRS.

---

# 3. Scope of Monitoring

System Exception Monitoring ditujukan untuk memantau berbagai kondisi tidak normal yang dapat diidentifikasi melalui pemeriksaan sistem.

Ruang lingkup pemantauan meliputi:

## 3.1. Data Integrity

Memastikan data yang dihasilkan oleh sistem memenuhi aturan konsistensi dan integritas yang telah ditentukan.

Contoh:
- Jurnal akuntansi tidak balance.
- Piutang terbentuk lebih dari satu kali untuk transaksi sumber yang sama.
- Data transaksi tidak memiliki data pendukung yang seharusnya terbentuk.
- Ketidaksesuaian hubungan antara transaksi sumber dan hasil pemrosesannya.

## 3.2. Process Integrity

Memastikan proses otomatis maupun semi-otomatis berjalan dan menghasilkan kondisi akhir yang diharapkan.

Contoh:
- Proses generate jurnal tidak menghasilkan jurnal.
- Proses pembentukan piutang tidak berjalan.
- Proses otomatis menghasilkan data yang tidak sesuai dengan transaksi sumber.
- Proses tertentu berhenti sebelum mencapai kondisi selesai.

## 3.3. Data Synchronization

Memastikan proses sinkronisasi data antar-modul maupun dengan sistem eksternal berjalan dengan baik.

Contoh:
- Kegagalan sinkronisasi data EMR.
- Perbedaan jadwal dokter antara SIMRS dan HFIS.
- Data yang seharusnya tersinkronisasi tidak ditemukan pada sistem tujuan.
- Data antar-sistem mengalami ketidaksesuaian.

## 3.4. Infrastructure and Service Monitoring

Memastikan layanan pendukung operasional sistem berjalan sesuai kondisi yang diharapkan.

Contoh:
- Remote print server tidak memproses antrian cetak.
- Antrian cetak tidak mengalami pergerakan dalam periode tertentu.
- Service pendukung berhenti atau tidak merespons.
- Proses terjadwal tidak berjalan sesuai jadwal.

Ruang lingkup tersebut dapat diperluas seiring ditemukannya jenis permasalahan baru yang dapat didefinisikan dan dideteksi secara sistematis.

---

# 4. Core Concepts

System Exception Monitoring terdiri dari dua bagian utama:

1. Detection Layer
2. Management Layer

Keduanya memiliki tanggung jawab yang berbeda dan berkomunikasi melalui mekanisme pelaporan yang distandarkan.

## 4.1. Detector

Detector adalah komponen independen yang bertanggung jawab melakukan pemeriksaan untuk mengidentifikasi satu jenis permasalahan tertentu.

Setiap Detector memiliki aturan pemeriksaan, konfigurasi, dan mekanisme eksekusi sendiri.

Karakteristik Detector:

- Memiliki identitas dan deskripsi yang jelas.
- Bertanggung jawab terhadap satu jenis permasalahan.
- Dapat dikembangkan dan dipelihara secara independen.
- Dapat dijalankan secara otomatis maupun manual.
- Menghasilkan laporan dalam format standar.
- Tidak bertanggung jawab mengelola proses investigasi dan penyelesaian masalah.

Contoh:
- Journal Balance Detector.
- Duplicate Receivable Detector.
- EMR Synchronization Detector.
- Doctor Schedule Synchronization Detector.
- Print Queue Detector.

## 4.2. Trigger

Trigger adalah mekanisme yang menentukan kapan suatu Detector dijalankan.

Jenis trigger meliputi:

**Automatic Trigger**
- Pemeriksaan berdasarkan interval waktu tertentu.
- Pemeriksaan berdasarkan jadwal yang telah ditentukan.
- Pemeriksaan sebagai respons terhadap proses tertentu yang selesai dijalankan.

**Manual Trigger**
- Pemeriksaan dijalankan atas permintaan petugas melalui mekanisme yang disediakan sistem.

Trigger hanya bertanggung jawab memulai eksekusi Detector. Aturan pemeriksaan tetap menjadi tanggung jawab Detector.

## 4.3. Finding

Finding adalah hasil pemeriksaan Detector yang menunjukkan adanya kondisi tidak normal.

Setiap Finding harus menyediakan informasi yang cukup untuk mengidentifikasi dan memahami permasalahan yang ditemukan.

Informasi dapat meliputi:
- Jenis permasalahan.
- Deskripsi kondisi tidak normal.
- Waktu ditemukan.
- Identitas data atau transaksi terkait.
- Tingkat keparahan.
- Bukti atau konteks pendukung.

Finding merupakan hasil deteksi, bukan representasi proses penanganan masalah.

## 4.4. Issue

Issue adalah representasi permasalahan yang harus ditindaklanjuti oleh petugas.

Satu Issue dapat memiliki beberapa Finding atau Occurrence apabila pemeriksaan berulang menemukan permasalahan yang sama.

Sistem harus mampu membedakan permasalahan baru dengan temuan berulang agar Dashboard tidak dipenuhi laporan duplikat.

Issue menjadi objek utama dalam proses investigasi, penanganan, dan penyelesaian.

## 4.5. Occurrence

Occurrence adalah catatan setiap kejadian ketika Detector menemukan suatu permasalahan.

Occurrence digunakan untuk menyimpan riwayat temuan, termasuk waktu dan bukti pemeriksaan.

Beberapa Occurrence dapat dikaitkan dengan satu Issue apabila semuanya merepresentasikan permasalahan yang sama.

Pemisahan Issue dan Occurrence diperlukan untuk mempertahankan riwayat pemeriksaan tanpa menghasilkan terlalu banyak Issue duplikat.

## 4.6. Issue Resolution

Issue Resolution adalah proses penanganan dan penyelesaian permasalahan oleh petugas yang berwenang.

Proses ini mencakup:
- Investigasi penyebab masalah.
- Penentuan tindakan perbaikan.
- Pelaksanaan perbaikan berdasarkan SOP.
- Pencatatan hasil tindakan.
- Verifikasi kondisi setelah perbaikan.

Setiap proses penyelesaian harus memiliki informasi yang dapat ditelusuri, termasuk petugas dan waktu pelaksanaannya.

---

# 5. Operational Workflow

Alur operasional System Exception Monitoring adalah sebagai berikut:

## 5.1. Detection

Detector dijalankan berdasarkan trigger yang telah ditentukan.

Detector melakukan pemeriksaan terhadap data, transaksi, proses, atau layanan yang menjadi tanggung jawabnya.

## 5.2. Finding Generation

Apabila ditemukan kondisi tidak normal, Detector menghasilkan Finding yang berisi informasi permasalahan dan bukti pendukung.

Apabila tidak ditemukan permasalahan, pemeriksaan dicatat sebagai pemeriksaan berhasil tanpa temuan.

## 5.3. Issue Reporting

Finding dikirimkan kepada System Exception Monitoring melalui mekanisme pelaporan yang telah distandarkan.

Sistem menerima dan memvalidasi laporan, kemudian menentukan apakah laporan tersebut merupakan Issue baru atau Occurrence dari Issue yang sudah ada.

## 5.4. Issue Management

Issue yang terbentuk ditampilkan pada Dashboard.

Petugas EDP dapat:
- Melihat daftar Issue.
- Memeriksa detail dan bukti permasalahan.
- Melakukan investigasi.
- Menetapkan petugas penanggung jawab.
- Mencatat tindakan penanganan.
- Memperbarui status permasalahan.

## 5.5. Resolution and Verification

Setelah tindakan perbaikan dilakukan, petugas mencatat hasil penyelesaian.

Kondisi setelah perbaikan perlu diverifikasi untuk memastikan permasalahan benar-benar telah teratasi.

Verifikasi dapat dilakukan melalui pemeriksaan ulang oleh Detector maupun pemeriksaan manual apabila diperlukan.

## 5.6. Historical Tracking

Seluruh proses deteksi, pelaporan, investigasi, dan penyelesaian disimpan sebagai riwayat.

Informasi tersebut dapat digunakan untuk mengevaluasi frekuensi permasalahan, efektivitas penanganan, dan kualitas operasional sistem.

---

# 6. Issue Lifecycle

Setiap Issue dikelola melalui siklus penanganan yang terdefinisi.

Status utama yang digunakan:

| Status | Description |
|---|---|
| Open | Permasalahan baru ditemukan dan menunggu penanganan. |
| Acknowledged | Permasalahan telah diketahui dan diterima oleh petugas. |
| Investigating | Permasalahan sedang diperiksa dan ditangani. |
| Resolved | Perbaikan telah dilakukan dan hasilnya telah diverifikasi. |
| Reopened | Permasalahan ditemukan kembali setelah sebelumnya dinyatakan selesai. |

Perubahan status harus tercatat dalam riwayat aktivitas Issue.

Issue yang ditemukan kembali setelah penyelesaian harus dapat dikaitkan dengan riwayat permasalahan sebelumnya.

---

# 7. System Architecture Principles

Pengembangan System Exception Monitoring harus mengikuti prinsip arsitektur berikut.

## 7.1. Independent Detection

Setiap Detector memiliki tanggung jawab deteksi yang spesifik dan tidak bergantung pada implementasi Dashboard.

## 7.2. Extensibility

Sistem harus memungkinkan penambahan Detector baru tanpa memerlukan perubahan pada aplikasi utama, khususnya Reporting API dan Dashboard, selama Detector mengikuti kontrak komunikasi yang telah ditetapkan.

## 7.3. Standardized Communication

Seluruh Detector menggunakan standar komunikasi dan format laporan yang sama untuk mengirimkan hasil pemeriksaan.

## 7.4. Separation of Responsibilities

Tanggung jawab sistem dibagi secara jelas:

- Detector bertanggung jawab mendeteksi permasalahan.
- Monitoring Application bertanggung jawab mengelola Issue.
- Petugas EDP bertanggung jawab melakukan investigasi dan perbaikan.

## 7.5. Data Traceability

Setiap hasil pemeriksaan, Finding, Issue, aktivitas penanganan, dan penyelesaian harus dapat ditelusuri.

## 7.6. Execution Reliability

Sistem harus mampu mencatat keberhasilan maupun kegagalan eksekusi Detector.

Kegagalan Detector tidak boleh dianggap sebagai hasil pemeriksaan normal.

## 7.7. Operational Safety

Proses pemeriksaan tidak boleh mengganggu kinerja operasional SIMRS maupun mengubah data transaksi yang sedang diproses.

Detector pada tahap awal berfungsi sebagai mekanisme observasi dan pelaporan, bukan sebagai mekanisme perbaikan otomatis.

---

# 8. Main Application Capabilities

System Exception Monitoring sekurang-kurangnya menyediakan kemampuan berikut.

| Capability | Description |
|---|---|
| Detector Registry | Mengelola identitas dan informasi Detector yang terpasang. |
| Detector Execution | Menjalankan Detector melalui trigger otomatis maupun manual. |
| Execution Monitoring | Memantau status dan riwayat eksekusi Detector. |
| Reporting API | Menerima dan memvalidasi laporan dari Detector. |
| Issue Management | Membentuk dan mengelola Issue berdasarkan hasil deteksi. |
| Occurrence Tracking | Menyimpan riwayat temuan berulang. |
| Issue Dashboard | Menampilkan seluruh Issue yang perlu diperhatikan. |
| Issue Investigation | Mendukung proses investigasi dan pencatatan hasil pemeriksaan. |
| Issue Assignment | Mengelola penugasan petugas penanggung jawab. |
| Resolution Tracking | Mencatat tindakan dan hasil penyelesaian. |
| Verification | Mendukung pemeriksaan ulang setelah perbaikan. |
| Historical Reporting | Menyediakan data historis untuk evaluasi kualitas sistem. |

---

# 9. Deployment Concept

System Exception Monitoring dirancang untuk dipasang pada lingkungan operasional masing-masing rumah sakit.

Detector dapat mengakses database, service, maupun sumber data lokal yang diperlukan untuk melakukan pemeriksaan.

Seluruh hasil pemeriksaan dikirimkan kepada Monitoring Application yang terpasang di lingkungan rumah sakit tersebut.

Petugas EDP rumah sakit menggunakan Dashboard untuk mengelola temuan dan melakukan tindak lanjut.

Akses remote oleh tim ICS dapat disediakan sesuai kebijakan keamanan dan infrastruktur masing-masing rumah sakit.

Setiap instalasi rumah sakit memiliki lingkungan monitoring dan penyimpanan data sendiri.

---

# 10. Success Criteria

Keberhasilan implementasi System Exception Monitoring diukur berdasarkan kemampuan sistem dalam memenuhi kondisi berikut:

1. Permasalahan yang dapat didefinisikan dan dideteksi dapat ditemukan tanpa harus menunggu laporan pengguna.
2. Pemeriksaan dapat dilakukan secara otomatis maupun manual.
3. Detector baru dapat ditambahkan tanpa mengubah aplikasi utama.
4. Temuan berulang dapat dikelola tanpa menghasilkan Issue duplikat yang tidak diperlukan.
5. Petugas dapat mengidentifikasi, menginvestigasi, dan menindaklanjuti setiap Issue.
6. Seluruh aktivitas penanganan dan penyelesaian memiliki riwayat yang dapat ditelusuri.
7. Kegagalan eksekusi Detector dapat diketahui dan ditindaklanjuti.
8. Sistem dapat membantu mengurangi akumulasi permasalahan yang baru diketahui pada akhir periode operasional.

---

# 11. Product Boundary

System Exception Monitoring berfokus pada deteksi dini dan pengelolaan permasalahan yang ditemukan melalui pemeriksaan sistem.

Sistem ini bukan pengganti mekanisme validasi transaksi pada SIMRS, bukan aplikasi pencatatan bug secara manual oleh pengguna, dan bukan sistem untuk melakukan perbaikan otomatis terhadap seluruh permasalahan.

Validasi transaksi tetap menjadi tanggung jawab masing-masing modul SIMRS.

System Exception Monitoring berfungsi sebagai lapisan pemantauan tambahan untuk menemukan kondisi tidak normal yang tidak dapat dicegah atau diidentifikasi secara langsung pada saat proses transaksi berlangsung.

Dengan demikian, sistem ini melengkapi mekanisme pengendalian yang sudah ada melalui pemeriksaan berkelanjutan terhadap hasil operasional SIMRS.

---

# 12. Product Vision

Mewujudkan ekosistem pemantauan kualitas SIMRS yang proaktif, extensible, dan berkelanjutan.

Setiap permasalahan operasional yang dapat diobservasi dan didefinisikan diharapkan memiliki mekanisme deteksi yang sesuai, sehingga permasalahan dapat diketahui sedini mungkin, ditangani secara sistematis, dan tidak terakumulasi menjadi beban operasional yang lebih besar.

**Core Principle:**

> Detect problems early, report automatically, resolve systematically, and continuously improve system quality.
