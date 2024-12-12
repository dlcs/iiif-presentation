# Populate

Python script to help in populating root collections for existing IIIF-CS customers. This is helpful when deploying IIIF-Presentation to an existing IIIF-CS instance that already has customers.

To run create a file `sample.json` at the same level as this readme that has format:

```json
[
    { "id": -1, "name": "first-customer" },
    { "id": -2, "name": "second-customer" }
]
```

and then update `CHANGEME` values in `create_roots.py` and run `python create_roots.py`. This will raise an SNS notification for each id/name value in the json array. This will be added to a queue and processed by IIIF-Presentation as if a new customer was created.
