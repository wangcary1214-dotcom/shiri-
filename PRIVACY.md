# Privacy

Shiri stores tasks, plans, schedules, focus history, diary text and images locally in the data folder beside the program. There is no account login, cloud synchronization or analytics service in the application code.

The embedded diary loads locally packaged scripts and fonts. It blocks external image/resource requests; raw Markdown HTML does not execute. HTTP(S) links open in the system browser only after the user clicks them. Local file import/export and backup occur when requested by the user. WebView2 maintains its local browser data in data/.journal-webview2; these browser profiles are excluded from Shiri's full local backup.

Dependencies/artwork are fetched from named public sources during development and CI builds, not by the diary renderer while editing user text. Microsoft WebView2 Runtime may have its own update and diagnostic behavior governed by Microsoft's terms and privacy policy: https://privacy.microsoft.com/privacystatement . Optional OCR components are separately licensed. Shiri's OCR integration sends images only to local OCR engines.

Do not upload data folders, private backups, browser profiles, local logs containing personal content, or signing credentials to this repository. Uninstall a portable Shiri build by closing it and removing its program folder; retain data separately if needed. Removing data also removes personal records.
