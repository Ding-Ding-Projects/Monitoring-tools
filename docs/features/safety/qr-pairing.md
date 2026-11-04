# QR pairing

**Not available in this build.**

## Reading a QR

Windows has no built-in QR decoder, and adding a decoder library is outside this change. The **Read a QR image** button therefore says plainly that it cannot, and **Paste from clipboard** tells you when the clipboard holds a picture it cannot read. Use the otpauth link instead: sign-in pages usually show it under a link such as "Can't scan?", and a phone QR scanner shows it as text that can be copied and pasted.

## Drawing a QR

The lock wizard does not draw a QR code for a lock's secret. It shows the grouped base32 secret (hidden until you ask), the algorithm, digits and period, and a **Copy otpauth link** button. Nothing is sent to any web service to make a code, and nothing about pairing touches the network. It requires the user to type back a current code before the lock arms.

## To finish later

An in-process QR encoder and decoder written from the specification, or an approved decoder dependency.
