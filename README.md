<div align="center">
  <h1>Anti-spam telegram bot</h1> 
</div>

Improved version of [telegram bot from TiraelSedai](https://github.com/TiraelSedai/ClubDoorman/) (thank you very much!) to protect telegram communities from spam. 

## 🌊 How to run 

First of all, you need to create a file `ClubDoorman/data/spam-ham.txt` (for example, you can take it [from here](https://github.com/TiraelSedai/ClubDoorman/blob/main/ClubDoorman/data/spam-ham.txt)). It is needed to train the ML classifier. Also you can add files `ClubDoorman/data/stop-words.txt` and `ClubDoorman/data/stop-reget.txt` for auto-ban on certain words or regular expressions. 

After that, you have to set environment variables (optional variables are highlighted in italics): 

* `DOORMAN_BOT_API`: Telegram bot token. 
* `DOORMAN_ADMIN_CHAT`: The chat ID of the admin chat.
* _`DOORMAN_BLACKLIST_AUTOBAN_DISABLE`: Set to true or 1 so that the bot does not ban people when joining chat._
* _`DOORMAN_LOW_CONFIDENCE_HAM_ENABLE`: Set to true or 1 to send to the admin chat non-spam messages with low confidentiality rate._
* _`DOORMAN_CHANNELS_AUTOBAN_DISABLE`: Set to true or 1 so that the bot does not ban messages from channels._
* _`DOORMAN_APPROVE_BUTTON`: Set to true or 1 so that the option "this is a friend" is added to each deleted message._
* _`DOORMAN_LOOKALIKE_AUTOBAN_DISABLE`: Set to true or 1 so that the bot does not ban messages with words masquerading as Russian._
* _`DOORMAN_BUTTON_AUTOBAN_DISABLE`: Set to true or 1 so that the bot does not ban messages where there are buttons._
* _`DOORMAN_HIGH_CONFIDENCE_AUTOBAN_DISABLE`: Set to true or 1 so that the bot does not ban messages where the ML classifier is confident enough about spam._
* _`DOORMAN_CHANNEL_AUTOBI_EXCLUSION`: Comma-separated groups in which channels are not banned._

In the end, you can use docker. 

```
docker compose build
docker compose up -d
```

## 🔮 How does it ban? 

_TL;DR: Checks the user's ID against an open database,  checks the message through stopwords and ML classifier, and does not ban those who have already written good messages._

1. When entering the chat, it gives you a simple captcha.
2. If the user is already trusted, then all checks are skipped. This condition is disabled in the current version.
3. If the user is on [known spammer lists](https://lols.bot/), the message is deleted.
4. If there are more than 5 emojis in the message, it is deleted.
5. If the message contains words that disguise themselves as Russian but have English letters inside, it is deleted. The most common English letters found in Slavic languages are not counted (for example, i).
6. If the message contains stop words, it is deleted.
7. Then the message is cleaned of emojis, punctuation, diacritics and checked by ML. If ML thinks it's spam, the message is deleted.
8. If the user has written several normal messages, they are added to the trusted ones.
9. Automatically, the bot only deletes messages and gives read-only for an hour. Users are banned only manually from the admin chat to reduce false positives.

Often, a bot needs to be retrained for the specifics of a particular chat. In this case, you can use the following commands in the admin chat. They are triggered by replaying the user's message.

* `/spam` adds a message to the spam dataset
* `/ham` adds a message to the non-spam dataset
* `/check` checks how the bot responds to such a message. 

If a new message appears in the spam dataset, the bot will re-learn on the entire dataset within a minute. If you are not ready to wait, use `docker compose restart`. 
